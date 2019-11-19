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
        readonly static string _logfile = "scrape_log.txt";

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
                    listing.Title = searchItem.title;
                    listing.ItemID = searchItem.itemId;
                    listing.EbayUrl = searchItem.viewItemURL;
                    listing.PrimaryCategoryID = searchItem.primaryCategory.categoryId;
                    listing.PrimaryCategoryName = searchItem.primaryCategory.categoryName;
                    listing.SellerPrice = (decimal)searchItem.sellingStatus.currentPrice.Value;
                    listing.Variation = searchItem.isMultiVariationListing;
                    listings.Add(listing);
                }
                return listings;
            }
            catch (Exception exc)
            {
                string msg = " StoreTransactionsManual " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, "");
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static ModelView ScanSeller(UserSettingsView settings, string seller, int daysBack, bool getTransactionHistory)
        {
            dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
            int notSold = 0;
            var listings = new List<Listing>();
            try
            {
                CustomFindSold service = new CustomFindSold();
                service.Url = "http://svcs.ebay.com/services/search/FindingService/v1";
                service.appID = settings.AppID;
                int currentPageNumber = 1;

                var request = ebayAPIs.BuildReqest(seller, daysBack);
                dsutil.DSUtil.WriteFile(_logfile, "Retrieve sales for " + seller, settings.UserName);
                var response = GetCompletedItems(service, request, currentPageNumber);
                dsutil.DSUtil.WriteFile(_logfile, "Retrieve sales complete", settings.UserName);

                if (response.ack == AckValue.Success)
                {
                    var result = response.searchResult;
                    if (result != null && result.count > 0)
                    {
                        listings = MapSearchResultToListing(result);

                        // are there more pages of results?
                        for (var i = response.paginationOutput.pageNumber; i < response.paginationOutput.totalPages; i++)
                        {
                            currentPageNumber += 1;
                            response = GetCompletedItems(service, request, currentPageNumber);
                            result = response.searchResult;
                            listings = MapSearchResultToListing(result);
                        }
                    }
                    var mv = new ModelView();
                    mv.Listings = listings;

                    int b = notSold;
                    return mv;
                }
                return null;
            }
            catch (Exception exc)
            {
                string msg = " ScanSeller " + exc.Message;
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

        public static List<OrderHistoryDetail> GetTransactionsFromPage(string html)
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
                                    dateSold = dateSold.Replace(" PST", "").Replace(" PDT", "");
                                    //tran.DateSold = DateTime.ParseExact(dateSold, "MMM-dd-yy hh:mm:ss", CultureInfo.InvariantCulture);
                                    DateTime dateTime;
                                    bool r = DateTime.TryParse(dateSold, out dateTime);
                                    tran.DateOfPurchase = dateTime;
                                    transactions.Add(tran);
                                    element = 0;
                                    break;
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

    }
}
