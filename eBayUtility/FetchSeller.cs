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
                    //if (searchItem.itemId == "303200616411")
                    //{
                    //    int stop = 99;
                    //}
                    var listing = new Listing();
                    var sellerListing = new SellerListing();

                    sellerListing.Title = searchItem.title;
                    listing.ItemID = searchItem.itemId;
                    sellerListing.EbayURL = searchItem.viewItemURL;
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
                else
                {
                    dsutil.DSUtil.WriteFile(_logfile, "ScanSeller: " + seller + " - GetCompletedItems returned Failure", "admin");
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
            try
            {
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
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="rptNumber">Pass either rptNumber or storeID.</param>
        /// <param name="minSold"></param>
        /// <param name="daysBack"></param>
        /// <param name="minPrice"></param>
        /// <param name="maxPrice"></param>
        /// <param name="activeStatusOnly"></param>
        /// <param name="isSellerVariation"></param>
        /// <param name="itemID"></param>
        /// <param name="pctProfit"></param>
        /// <param name="storeID">Pass storeID to run all sellers in store.</param>
        /// <returns></returns>
        public static async Task<string> CalculateMatch(UserSettingsView settings, int rptNumber, int minSold, int daysBack, int? minPrice, int? maxPrice, bool? activeStatusOnly, bool? isSellerVariation, string itemID, double pctProfit, int storeID)
        {
            string ret = null;
            var sh = new SearchHistory();
            if (rptNumber > 0 && storeID > 0)
            {
                ret = "Invalid call to FillMatch.";
                dsutil.DSUtil.WriteFile(_logfile, ret, "");
            }
            else if (rptNumber > 0)
            {
                sh.ID = rptNumber;
                sh.CalculateMatch = DateTime.Now;
                models.SearchHistoryUpdate(sh, "CalculateMatch");
                var mv = await FillMatch(settings, rptNumber, minSold, daysBack, minPrice, maxPrice, activeStatusOnly, isSellerVariation, itemID, 5);
            }
            else if (storeID > 0)
            {
                var sellers = models.GetSellers(storeID);
                bool runScan = false;
                foreach (var seller in sellers)
                {
                    //if (seller.Seller == "exxbargain")
                    //{
                    //    int stop = 99;
                    //}
                    //else
                    //{
                    //    continue;
                    //}
                    runScan = false;
                    var sellerProfile = await models.SellerProfileGet(seller.Seller);
                    if (sellerProfile == null)
                    {
                        runScan = true;
                    }
                    else
                    {
                        if (sellerProfile.Active)
                        {
                            runScan = true;
                        }
                    }
                    if (runScan)
                    {
                        if (seller.CalculateMatch == null || seller.CalculateMatch < seller.Updated)
                        {
                            Console.WriteLine(seller.Seller);
                            sh.ID = seller.ID;
                            sh.CalculateMatch = DateTime.Now;
                            models.SearchHistoryUpdate(sh, "CalculateMatch");
                            var mv = await FetchSeller.FillMatch(settings, seller.ID, minSold, daysBack, minPrice, maxPrice, activeStatusOnly, isSellerVariation, itemID, 5);
                            dsutil.DSUtil.WriteFile(_logfile, seller.Seller + ": Ran FillMatch", "");
                            Thread.Sleep(2000);
                        }
                    }
                }
            }
            else
            {
                ret = "Invalid call to FillMatch.";
                dsutil.DSUtil.WriteFile(_logfile, ret, "");
            }
            return ret;
        }
        /// <summary>
        /// Based on filtering a seller's sales, try to match a prodID with a prodID on walmart
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="rptNumber"></param>
        /// <param name="minSold"></param>
        /// <param name="daysBack"></param>
        /// <param name="minPrice"></param>
        /// <param name="maxPrice"></param>
        /// <param name="activeStatusOnly"></param>
        /// <param name="nonVariation"></param>
        /// <param name="itemID"></param>
        /// <param name="pctProfit"></param>
        /// <returns></returns>
        private static async Task<ModelViewTimesSold> FillMatch(UserSettingsView settings, int rptNumber, int minSold, int daysBack, int? minPrice, int? maxPrice, bool? activeStatusOnly, bool? isSellerVariation, string itemID, double pctProfit)
        {
            string loopItemID = null;
            try
            {
                DateTime ModTimeTo = DateTime.Now.ToUniversalTime();
                DateTime ModTimeFrom = ModTimeTo.AddDays(-daysBack);

                itemID = (itemID == "null") ? null : itemID;
                var x = models.GetSalesData(rptNumber, ModTimeFrom, settings.StoreID, itemID);

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
                if (isSellerVariation.HasValue)
                {
                    if (isSellerVariation.Value)
                    {
                        x = x.Where(p => !p.IsSellerVariation.Value);
                    }
                }
                var mv = new ModelViewTimesSold();
                mv.TimesSoldRpt = x.ToList();
                foreach (var row in mv.TimesSoldRpt)
                {
                    loopItemID = row.ItemID;
                    if (row.ItemID == "392551593836")
                    {
                        int stop = 99;
                    }
                    WalmartSearchProdIDResponse response;
                    var walitem = new SupplierItem();
                    if (row.SellerUPC != null)
                    {
                        response = wallib.wmUtility.SearchProdID(row.SellerUPC);
                        if (response.Count == 1)
                        {
                            //if (row.SellerUPC == "081483818559")
                            //{
                            //    int stop = 99;
                            //}
                            walitem = await wallib.wmUtility.GetDetail(response.URL);
                            walitem.MatchCount = response.Count;
                            walitem.UPC = row.SellerUPC;
                            walitem.MatchType = 1;
                            walitem.Updated = DateTime.Now;
                            models.SupplierItemUpdateScrape(row.SellerUPC, "", walitem, 
                                "MatchCount",
                                "Updated",
                                "MatchType",
                                "ItemURL",
                                "SoldAndShippedBySupplier",
                                "SupplierBrand",
                                "SupplierPrice",
                                "IsVariation",
                                "SupplierPicURL");

                            if (walitem.SupplierPrice.HasValue)
                            {
                                var oh = new OrderHistory();
                                oh.ItemID = row.ItemID;
                                var p = Utility.eBayItem.wmNewPrice(walitem.SupplierPrice.Value, 6);
                                oh.ProposePrice = p;
                                models.OrderHistoryUpdate(oh, "ProposePrice");
                            }
                        }
                    }
                    else
                    {
                        if (row.SellerMPN != null)
                        {
                            response = wallib.wmUtility.SearchProdID(row.SellerMPN);
                            if (response.Count == 1)
                            {
                                walitem = await wallib.wmUtility.GetDetail(response.URL);
                                walitem.MatchCount = response.Count;
                                walitem.MPN = row.SellerMPN;
                                walitem.MatchType = 2;
                                walitem.Updated = DateTime.Now;
                                models.SupplierItemUpdateScrape("", row.SellerMPN, walitem,
                                    "MatchCount",
                                    "Updated",
                                    "MatchType",
                                    "ItemURL",
                                    "SoldAndShippedBySupplier",
                                    "SupplierBrand",
                                    "SupplierPrice",
                                    "IsVariation",
                                    "SupplierPicURL");

                                // now update the ebay seller item specific UPC
                                // but walmart doesn't always give a UPC
                                if (!string.IsNullOrEmpty(walitem.UPC))
                                {
                                    var itemSpecific = new OrderHistoryItemSpecific();
                                    itemSpecific.SellerItemID = row.ItemID;
                                    itemSpecific.ItemName = "UPC";
                                    itemSpecific.ItemValue = walitem.UPC;
                                    itemSpecific.Flags = true;
                                    models.OrderHistoryItemSpecificUpdate(itemSpecific);
                                }

                                if (walitem.SupplierPrice.HasValue)
                                {
                                    var oh = new OrderHistory();
                                    oh.ItemID = row.ItemID;
                                    var p = Utility.eBayItem.wmNewPrice(walitem.SupplierPrice.Value, 6);
                                    oh.ProposePrice = p;
                                    models.OrderHistoryUpdate(oh, "ProposePrice");
                                }
                            }
                        }
                    }
                }
                return mv;
            }
            catch (Exception exc)
            {
                string msgItemID = (!string.IsNullOrEmpty(loopItemID)) ? "ItemID: " + loopItemID : "";
                string header = "FillMatch RptNumber: " + rptNumber.ToString() + " " + msgItemID;
                string msg = dsutil.DSUtil.ErrMsg(header, exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "");
                return null;
            }
        }
        /// <summary>
        /// This is where a SellerListing record is created.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="rptNumber"></param>
        /// <returns></returns>
        public static async Task<string> StoreToListing(UserSettingsView settings)
        {
            string ret = "Copied 0 records.";
            int copiedRecords = 0;
            try
            {
                //var searchHistory = models.SearchHistory.Find(rptNumber);
                var recs = models.OrderHistory.AsNoTracking().Include("SearchHistory").Include("ItemSpecifics").Where(p => p.ToListing ?? false).ToList();
                foreach (var oh in recs)
                {
                    var UPC = oh.ItemSpecifics.Where(p => p.ItemName == "UPC").Select(q => q.ItemValue).SingleOrDefault();
                    var MPN = oh.ItemSpecifics.Where(p => p.ItemName == "MPN").Select(q => q.ItemValue).SingleOrDefault();
                    string foundResult = models.ProdIDExists(UPC, MPN, settings.StoreID);
                    if (foundResult == null)
                    {
                        var listing = new Listing();
                        listing.ItemID = oh.ItemID;
                        listing.ListingTitle = oh.Title;
                        if (oh.ProposePrice.HasValue)
                        {
                            listing.ListingPrice = oh.ProposePrice.Value;
                        }
                        var supplierItem = models.GetSupplierItem(oh.ItemID);
                        listing.SourceUrl = supplierItem.ItemURL;
                        //listing.SupplierPrice = oh.WMPrice.Value;
                        //listing.PictureUrl = oh.WMPicUrl;
                        listing.Profit = 0;
                        listing.ProfitMargin = 0;
                        listing.StoreID = settings.StoreID;
                        listing.Description = supplierItem.Description;
                        var upc = models.OrderHistoryItemSpecifics.AsNoTracking().Where(i => i.SellerItemID == oh.ItemID && i.ItemName == "UPC").SingleOrDefault();
                        if (upc != null)
                        {
                            listing.UPC = upc.ItemValue;
                        }
                        var mpn = models.OrderHistoryItemSpecifics.AsNoTracking().Where(i => i.SellerItemID == oh.ItemID && i.ItemName == "MPN").SingleOrDefault();
                        if (mpn != null)
                        {
                            listing.MPN = mpn.ItemValue;
                        }
                        var sellerListing = new SellerListing();
                        sellerListing.ItemID = oh.ItemID;
                        sellerListing.Title = oh.Title;
                        sellerListing.Seller = oh.SearchHistory.Seller;
                        var si = await eBayUtility.ebayAPIs.GetSingleItem(settings, listing.ItemID);
                        sellerListing.PrimaryCategoryID = si.PrimaryCategoryID;
                        sellerListing.PrimaryCategoryName = si.PrimaryCategoryName;
                        sellerListing.Description = si.Description;
                        sellerListing.ListingStatus = si.ListingStatus;
                        sellerListing.EbayURL = si.EbayURL;
                        sellerListing.PictureURL = si.PictureURL;
                        sellerListing.SellerPrice = si.SellerPrice;
                        sellerListing.Updated = DateTime.Now;
                        listing.PrimaryCategoryID = si.PrimaryCategoryID;
                        listing.PrimaryCategoryName = si.PrimaryCategoryName;
                        sellerListing.ItemSpecifics = dsmodels.DataModelsDB.CopyFromOrderHistory(oh.ItemSpecifics);
                        listing.SellerListing = sellerListing;
                        listing.SupplierID = supplierItem.ID;

                        await models.ListingSaveAsync(listing, settings.UserID,
                            "SupplierItem.SupplierPrice",
                            "ListingPrice",
                            "ListingTitle",
                            "Description",
                            "Qty",
                            "Profit",
                            "ProfitMargin",
                            "UpdatedBy");

                        oh.ToListing = false;
                        models.OrderHistoryUpdate(oh, "ToListing");
                        ++copiedRecords;
                        ret = "Copied records: " + copiedRecords.ToString();
                    }
                    else
                    {
                        return foundResult;
                    }
                }
            }
            catch (Exception exc)
            {
                ret = dsutil.DSUtil.ErrMsg("StoreToListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, ret, "admin");
            }
            return ret;
        }

    }
}
