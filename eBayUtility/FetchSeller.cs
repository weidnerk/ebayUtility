/*
 * Fetch seller
 * 
 * 
 */
using dsmodels;
using eBayUtility.WebReference;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utility;
using wallib.Models;

namespace eBayUtility
{

    public class Transaction
    {
        public string Price { get; set; }
        public int Qty { get; set; }
        public DateTime DateSold { get; set; }
    }

    public static class FetchSeller
    {
        readonly static string _logfile = "log.txt";
        static dsmodels.DataModelsDB models = new dsmodels.DataModelsDB();
        /// <summary>
        /// GetCompletedItems(service, request, currentPageNumber) returns a FindCompletedItemsResponse which has property, searchResult
        /// Iterate the seller's sold items, fetching sales history
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public static List<Listing> MapSearchResultToListing(SearchResult result)
        {
            var listings = new List<Listing>();
            try
            {
                // Iterate completed items
                foreach (SearchItem searchItem in result.item)
                {
                    var listing = new Listing();
                    var sellerListing = new SellerListing();

                    sellerListing.Title = searchItem.title;
                    listing.ItemID = searchItem.itemId;
                    sellerListing.EbayUrl = searchItem.viewItemURL;
                    listing.PrimaryCategoryID = searchItem.primaryCategory.categoryId;
                    listing.PrimaryCategoryName = searchItem.primaryCategory.categoryName;
                    sellerListing.SellerPrice = (decimal)searchItem.sellingStatus.currentPrice.Value;
                    sellerListing.Variation = searchItem.isMultiVariationListing;
                    listing.SellerListing = sellerListing;
                    listings.Add(listing);
                }
                return listings;
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("MapSearchResultToListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "");
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static ModelView ScanSeller(UserSettingsView settings, string seller, DateTime fromDate)
        {
            dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
            int notSold = 0;
            var listings = new List<Listing>();
            var searchResult = new List<SearchResult>();
            try
            {
                CustomFindSold service = new CustomFindSold();
                service.Url = "http://svcs.ebay.com/services/search/FindingService/v1";
                service.appID = settings.AppID;
                int currentPageNumber = 1;

                var request = ebayAPIs.BuildReqest(seller, fromDate);
                dsutil.DSUtil.WriteFile(_logfile, "Retrieve sales for " + seller, settings.UserName);
                var response = GetCompletedItems(service, request, currentPageNumber);
                dsutil.DSUtil.WriteFile(_logfile, "Retrieve sales complete", settings.UserName);

                if (response.ack == AckValue.Success)
                {
                    var result = response.searchResult;
                    if (result != null && result.count > 0)
                    {
                        listings = MapSearchResultToListing(result);
                        searchResult.Add(result);

                        // are there more pages of results?
                        for (var i = response.paginationOutput.pageNumber; i < response.paginationOutput.totalPages; i++)
                        {
                            currentPageNumber += 1;
                            response = GetCompletedItems(service, request, currentPageNumber);
                            result = response.searchResult;
                            searchResult.Add(result);
                            var listingsNewPage = MapSearchResultToListing(result);
                            listings.AddRange(listingsNewPage);
                        }
                    }
                    var mv = new ModelView();
                    mv.Listings = listings;
                    mv.SearchResult = searchResult;

                    int b = notSold;
                    return mv;
                }
                return null;
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("ScanSeller", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return null;
            }
        }
        /// <summary>
        /// Actual fetch of seller's completed items
        /// </summary>
        /// <param name="service"></param>
        /// <param name="request"></param>
        /// <param name="currentPageNumber"></param>
        /// <returns>FindCompletedItemsResponse</returns>
        private static FindCompletedItemsResponse GetCompletedItems(CustomFindSold service, FindCompletedItemsRequest request, int currentPageNumber)
        {
            request.paginationInput = GetNextPage(currentPageNumber);
            return service.findCompletedItems(request);
        }

        private static PaginationInput GetNextPage(int pageNumber)
        {
            return new PaginationInput
            {
                entriesPerPageSpecified = true,
                entriesPerPage = 100,
                pageNumberSpecified = true,
                pageNumber = pageNumber
            };
        }
        private static string ParsePrice(string input)
        {
            int pos = input.IndexOf("$");
            if (pos > -1)
            {
                string price = input.Substring(pos + 1);
                return price;
            }
            return null;
        }

        /// <summary>
        /// See technique used in GetMPN()
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public static List<OrderHistoryDetail> GetTransactionsFromPage(string html, string itemID)
        {
            string dateSold = null;
            var transactions = new List<OrderHistoryDetail>();
            try
            {
                // https://stackoverflow.com/questions/4182594/grab-all-text-from-html-with-html-agility-pack
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

                int element = 0;
                foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//text()"))
                {
                    if (node.InnerText == "Purchase History Section")   // string match "Purchase History Section"
                    {
                        string price = null;
                        int qty = 0;

                        var secondTable = node.SelectSingleNode("//table[2]");  // move to 2nd table

                        foreach (HtmlNode nodet in secondTable.SelectNodes("//td[@class='contentValueFont']"))  // collect table elements matching class
                        {
                            string value = nodet.InnerText;
                            switch (element)
                            {
                                case 0:
                                    price = ParsePrice(value);
                                    ++element;
                                    break;
                                case 1:
                                    qty = Convert.ToInt32(value);
                                    ++element;
                                    break;
                                case 2:
                                    dateSold = value;
                                    var tran = new OrderHistoryDetail();
                                    tran.Price = Convert.ToDecimal(price);
                                    tran.Qty = qty;
                                    tran.DateOfPurchase = GetUTCFromEbayDateStr(dateSold);
                                    transactions.Add(tran);
                                    element = 0;
                                    break;
                            }
                        }
                        var variations = new List<string>();
                        var variationNode = secondTable.SelectNodes("//td[@class='variationContentValueFont']");
                        if (variationNode != null)
                        {
                            AddVariations(variationNode, transactions, itemID);
                        }
                    }
                }
                return transactions;
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetTransactionsFromPage", exc);
                dsutil.DSUtil.WriteFile(_logfile, itemID + ": " + msg, "");
                return null;
            }
        }

        /// <summary>
        /// Convert a purchase date string taken from ebay Purchase History page to a UTC date.
        /// </summary>
        /// <param name="dateTimeStr">Example: Nov-29-19 09:06:46 PST</param>
        /// <returns></returns>
        private static DateTime GetUTCFromEbayDateStr(string dateTimeStr)
        {
            string timeZoneStr = dateTimeStr.Substring(dateTimeStr.Length - 3, 3);
            dateTimeStr = dateTimeStr.Replace(" PST", "").Replace(" PDT", "");
            //tran.DateSold = DateTime.ParseExact(dateSold, "MMM-dd-yy hh:mm:ss", CultureInfo.InvariantCulture);
            DateTime dateTime;
            bool r = DateTime.TryParse(dateTimeStr, out dateTime);
            TimeZoneInfo timeZone = null;
            //if (timeZoneStr == "PDT")
            //{
            //    timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Daylight Time");
            //}
            //if (timeZoneStr == "PST")
            //{
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            //}
            var databaseUtcTime = TimeZoneInfo.ConvertTimeToUtc(dateTime, timeZone);
            return databaseUtcTime;
        }

        private static void AddVariations(HtmlNodeCollection variationNode, List<OrderHistoryDetail> transactions, string itemID)
        {
            try { 
                var variations = new List<string>();
                foreach (HtmlNode nodet in variationNode)
                {
                    var variation = nodet.InnerText;
                    if (!string.IsNullOrEmpty(variation))
                    {
                        variations.Add(variation);
                    }
                }
                if (variations.Count > 0)
                {
                    int variationCount = 0;
                    foreach (var t in transactions)
                    {
                        string variation = variations[variationCount++];
                        if (variation.Length > 50)  // not sure how long variation desription can be but db set at 50
                        {
                            t.Variation = variation.Substring(0, 50);
                        }
                        else
                        {
                            t.Variation = variation;
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("AddVariations", exc);
                dsutil.DSUtil.WriteFile(_logfile, itemID + ": " + msg, "");
            }
        }
        /// <summary>
        /// This was written in lieu of GetItemTransactions not working anymore.
        /// 
        /// After running this a number of times, eBay starts asking for a verification code, detecting a bot.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static async Task<List<Transaction>> GetTransactionsFromUrl(string url)
        {
            string dateSold = null;
            var transactions = new List<Transaction>();
            try
            {
                var cookieContainer = new CookieContainer();
                var handler = new HttpClientHandler();
                handler.CookieContainer = cookieContainer;
                var httpClient = new HttpClient(handler);
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/57.0.2987.133 Safari/537.36");

                using (httpClient)
                using (HttpResponseMessage response = await httpClient.GetAsync(url))
                using (HttpContent content = response.Content)
                {
                    // ... Read the string.
                    string result = await content.ReadAsStringAsync();
                    //Thread.Sleep(2000);
                    if (response.IsSuccessStatusCode)
                    {
                        // https://stackoverflow.com/questions/4182594/grab-all-text-from-html-with-html-agility-pack
                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(result);

                        int element = 0;
                        foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//text()"))
                        {
                            if (node.InnerText == "Purchase History Section")   // string match "Purchase History Section"
                            {
                                string price = null;
                                int qty = 0;

                                var secondTable = node.SelectSingleNode("//table[2]");  // move to 2nd table

                                foreach (HtmlNode nodet in secondTable.SelectNodes("//td[@class='contentValueFont']"))  // collect table elements matching class
                                {
                                    string value = nodet.InnerText;
                                    switch (element)
                                    {
                                        case 0:
                                            price = ParsePrice(value);
                                            ++element;
                                            break;
                                        case 1:
                                            qty = Convert.ToInt32(value);
                                            ++element;
                                            break;
                                        case 2:
                                            dateSold = value;
                                            var tran = new Transaction();
                                            tran.Price = price;
                                            tran.Qty = qty;
                                            dateSold = dateSold.Replace(" PST", "");
                                            //tran.DateSold = DateTime.ParseExact(dateSold, "MMM-dd-yy hh:mm:ss", CultureInfo.InvariantCulture);
                                            DateTime dateTime;
                                            bool r = DateTime.TryParse("Nov-14-19 18:06:07", out dateTime);
                                            tran.DateSold = dateTime;
                                            transactions.Add(tran);
                                            element = 0;
                                            break;
                                    }
                                }
                            }
                        }
                    }
                }
                return transactions;
            }
            catch (Exception exc)
            {
                string err = exc.Message;
                return null;
            }
        }
        /// <summary>
        /// Get seller's sold items for last 30 days, but no sales information as far as I can tell.
        /// </summary>
        /// <param name="seller"></param>
        //public static SearchItem[] GetSellersSoldItems(UserSettingsView settings, string seller)
        //{
        //    var result = ebayAPIs.FindCompletedItems(seller, 30, settings.AppID, 1);
        //    foreach (SearchItem x in result.searchResult.item)
        //    {
        //        Console.WriteLine(x.title);
        //    }
        //    return result.searchResult.item;
        //}

        public static async Task<ModelViewTimesSold> FillMatch(UserSettingsView settings, int rptNumber, int minSold, int daysBack, int? minPrice, int? maxPrice, bool? activeStatusOnly, bool? nonVariation, string itemID, double pctProfit)
        {
            try
            {
                DateTime ModTimeTo = DateTime.Now.ToUniversalTime();
                DateTime ModTimeFrom = ModTimeTo.AddDays(-daysBack);

                itemID = (itemID == "null") ? null : itemID;
                var x = models.GetScanData(rptNumber, ModTimeFrom, settings.StoreID, itemID: itemID);

                // filter by min and max price
                if (minPrice.HasValue)
                {
                    x = x.Where(p => p.Price >= minPrice);
                }
                if (maxPrice.HasValue)
                {
                    x = x.Where(p => p.Price <= maxPrice);
                }
                x = x.Where(p => p.SoldQty >= minSold);
                if (activeStatusOnly.HasValue)
                {
                    if (activeStatusOnly.Value)
                    {
                        x = x.Where(p => p.ListingStatus == "Active");
                    }
                }
                if (nonVariation.HasValue)
                {
                    if (nonVariation.Value)
                    {
                        x = x.Where(p => !p.IsMultiVariationListing.Value);
                    }
                }

                var mv = new ModelViewTimesSold();
                mv.TimesSoldRpt = x.ToList();
                foreach (var row in mv.TimesSoldRpt)
                {

                    //if (row.ItemID == "312767833884")
                    //{
                    //    int stop = 99;
                    //}

                    WalmartSearchProdIDResponse response;
                    if (row.UPC != null)
                    {
                        response = wallib.wmUtility.SearchProdID(row.UPC);
                        if (response.Count == 0)
                        {
                            if (row.MPN != null)
                            {
                                response = wallib.wmUtility.SearchProdID(row.MPN);
                            }
                        }
                        if (response.Count == 1)
                        {
                            var walitem = await wallib.wmUtility.GetDetail(response.URL);
                            response.SoldAndShippedByWalmart = walitem.FulfilledByWalmart;
                            response.SupplierBrand = walitem.Brand;
                            response.Price = walitem.Price;
                            response.IsVariation = walitem.IsVariation;
                            response.ProprosePrice = Utility.eBayItem.wmNewPrice(walitem.Price, pctProfit);
                            if (!string.IsNullOrEmpty(walitem.PictureUrl))
                            {
                                string[] arr = walitem.PictureUrl.Split(';');
                                response.Picture = arr[0];
                            }
                            else
                            {
                                dsutil.DSUtil.WriteFile(_logfile, itemID + ": (FillMatch) supplier images not available.", "");
                            }
                        }
                        models.OrderHistoryUpdate(rptNumber, row.ItemID, response);
                    }
                    else
                    {
                        if (row.MPN != null)
                        {
                            response = wallib.wmUtility.SearchProdID(row.MPN);
                            if (response.Count == 1)
                            {
                                var walitem = await wallib.wmUtility.GetDetail(response.URL);
                                response.SoldAndShippedByWalmart = walitem.FulfilledByWalmart;
                                response.SupplierBrand = walitem.Brand;
                                response.Price = walitem.Price;
                                response.IsVariation = walitem.IsVariation;
                                response.ProprosePrice = Utility.eBayItem.wmNewPrice(walitem.Price, pctProfit);

                                if (!string.IsNullOrEmpty(walitem.PictureUrl))
                                {
                                    string[] arr = walitem.PictureUrl.Split(';');
                                    response.Picture = arr[0];
                                }
                                else
                                {
                                    dsutil.DSUtil.WriteFile(_logfile, itemID + ": (FillMatch) supplier images not available.", "");
                                }
                            }
                            models.OrderHistoryUpdate(rptNumber, row.ItemID, response);
                        }
                    }
                }
                return mv;
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("FillMatch", exc);
                dsutil.DSUtil.WriteFile(_logfile, itemID + ": " + msg, "");
                return null;
            }
        }
        
    }
}
