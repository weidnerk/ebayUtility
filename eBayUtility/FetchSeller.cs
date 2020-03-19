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
using System.Data.Entity;
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
        /// GetCompletedItems for a seller.
        /// NOTE: we use the Listing class to house the seller's listing. 
        /// </summary>
        /// <returns></returns>
        public static ModelView ScanSeller(UserSettingsView settings, string seller, DateTime fromDate)
        {
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

        public static PaginationInput GetNextPage(int pageNumber)
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
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
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
        /// Are we calculating for just one seller or all active?
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
        public static async Task<string> CalculateMatch(UserSettingsView settings, int rptNumber, int minSold, int daysBack, int? minPrice, int? maxPrice, bool? activeStatusOnly, bool? isSellerVariation, string itemID, double pctProfit, int storeID, decimal wmShipping, decimal wmFreeShippingMin, double eBayPct, int imgLimit, string supplierTag)
        {
            string ret = null;
            try
            {
                var sh = new SearchHistory();
                if (rptNumber > 0 && storeID > 0)
                {
                    ret = "Invalid call to FillMatch.";
                    dsutil.DSUtil.WriteFile(_logfile, ret, "");
                }
                else if (rptNumber > 0) // Called from Display Scans - run a single reportID
                {
                    sh.ID = rptNumber;
                    sh.CalculateMatch = DateTime.Now;
                    models.SearchHistoryUpdate(sh, "CalculateMatch");
                    models.ClearOrderHistory(rptNumber);

                    await UPCMatch(settings, rptNumber, minSold, daysBack, minPrice, maxPrice, activeStatusOnly, isSellerVariation, itemID, pctProfit, wmShipping, wmFreeShippingMin, eBayPct, imgLimit);
                    
                    // 03.17.2020 currently not happy with accuracy
                    //await SearchEngineMatch(settings, rptNumber, minSold, daysBack, minPrice, maxPrice, activeStatusOnly, isSellerVariation, itemID, pctProfit, wmShipping, wmFreeShippingMin, eBayPct, imgLimit, supplierTag);
                }
                else if (storeID > 0)   // Used by CalculateMatch console app to run entire store
                {
                    var sellers = models.GetSellers();
                    bool runScan = false;
                    foreach (var seller in sellers)
                    {
                        Console.WriteLine(seller.Seller);
                     
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
                            int? latest = models.LatestRptNumber(seller.Seller);
                            var tgtSearchHistory = seller.SearchHistory.Where(p => p.ID == latest).SingleOrDefault();
                            if (tgtSearchHistory != null) 
                            {
                                if (tgtSearchHistory.CalculateMatch == null || tgtSearchHistory.CalculateMatch < tgtSearchHistory.Updated)
                                {
                                    sh.CalculateMatch = DateTime.Now;
                                    sh.Updated = DateTime.Now;
                                    sh.ID = tgtSearchHistory.ID;
                                    models.SearchHistoryUpdate(sh, "CalculateMatch", "Updated");
                                    
                                    await UPCMatch(settings, tgtSearchHistory.ID, minSold, daysBack, minPrice, maxPrice, activeStatusOnly, isSellerVariation, itemID, pctProfit, wmShipping, wmFreeShippingMin, eBayPct, imgLimit);
                                    
                                    //await SearchEngineMatch(settings, rptNumber, minSold, daysBack, minPrice, maxPrice, activeStatusOnly, isSellerVariation, itemID, pctProfit, wmShipping, wmFreeShippingMin, eBayPct, imgLimit, supplierTag);

                                    dsutil.DSUtil.WriteFile(_logfile, seller.Seller + ": Ran FillMatch", "");
                                }
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
            }
            catch (Exception exc)
            {
                ret = dsutil.DSUtil.ErrMsg("CalculateMatch", exc);
                dsutil.DSUtil.WriteFile(_logfile, ret, "admin");
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
        private static async Task UPCMatch(UserSettingsView settings, int rptNumber, int minSold, int daysBack, int? minPrice, int? maxPrice, bool? activeStatusOnly, bool? isSellerVariation, string itemID, double pctProfit, decimal wmShipping, decimal wmFreeShippingMin, double eBayPct, int imgLimit)
        {
            string loopItemID = null;
            try
            {
                var mv = new ModelViewTimesSold();
                mv.TimesSoldRpt = FilterMatch(settings, rptNumber, minSold, daysBack, minPrice, maxPrice, activeStatusOnly, isSellerVariation, itemID);

                foreach (var row in mv.TimesSoldRpt)
                {
                    loopItemID = row.ItemID;
                    bool tryAgain = false;
                    WalmartSearchProdIDResponse response;
                    var walitem = new SupplierItem();
                    if (row.SellerUPC != null)
                    {
                        response = wallib.wmUtility.SearchProdID(row.SellerUPC);
                        if (response.Count == 1)
                        {
                            walitem = await wallib.wmUtility.GetDetail(response.URL, imgLimit);

                            // If can't get supplier pics, not much point in posting.
                            // Can happen when not matching correctly on something like an eBook or giftcard where walmart
                            // is not providing "standard" images. (error is logged in GetDetail()).
                            if (!string.IsNullOrEmpty(walitem.SupplierPicURL))
                            {
                                walitem.UPC = row.SellerUPC;
                                walitem.Updated = DateTime.Now;
                                models.SupplierItemUpdateByProdID(row.SellerUPC, "", walitem,
                                    "Updated",
                                    "ItemURL",
                                    "SoldAndShippedBySupplier",
                                    "SupplierBrand",
                                    "SupplierPrice",
                                    "IsVariation",
                                    "SupplierPicURL",
                                    "IsFreightShipping");

                                var oh = new OrderHistory();
                                oh.ItemID = row.ItemID;
                                oh.MatchCount = response.Count;
                                oh.MatchType = 1;
                                oh.SourceID = walitem.SourceID;
                                oh.SupplierItemID = walitem.ID;
                                if (walitem.SupplierPrice.HasValue)
                                {
                                    var p = wallib.wmUtility.wmNewPrice(walitem.SupplierPrice.Value, pctProfit, wmShipping, wmFreeShippingMin, eBayPct);
                                    oh.ProposePrice = p.ProposePrice;
                                    models.OrderHistoryUpdate(oh, "ProposePrice", "MatchType", "MatchCount", "SourceID", "SupplierItemID");
                                }
                                else
                                {
                                    models.OrderHistoryUpdate(oh, "MatchType", "MatchCount", "SourceID", "SupplierItemID");
                                }
                            }
                        }
                        else
                        {
                            tryAgain = true;   
                        }
                    }
                    else
                    {
                        tryAgain = true;
                    }
                    if (tryAgain)
                    {
                        if (row.SellerMPN != null)
                        {
                            response = wallib.wmUtility.SearchProdID(row.SellerMPN);
                            if (response.Count == 1)
                            {
                                walitem = await wallib.wmUtility.GetDetail(response.URL, imgLimit);

                                // If can't get supplier pics, not much point in posting.
                                // Can happen when not matching correctly on something like an eBook or giftcard where walmart
                                // is not providing "standard" images. (error is logged in GetDetail()).
                                if (!string.IsNullOrEmpty(walitem.SupplierPicURL))
                                {
                                    walitem.MPN = row.SellerMPN;
                                    walitem.Updated = DateTime.Now;
                                    models.SupplierItemUpdateByProdID("", row.SellerMPN, walitem,
                                        "Updated",
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
                                        var p = wallib.wmUtility.wmNewPrice(walitem.SupplierPrice.Value, pctProfit, wmShipping, wmFreeShippingMin, eBayPct);
                                        oh.ProposePrice = p.ProposePrice;
                                        oh.MatchCount = response.Count;
                                        oh.MatchType = 1;
                                        oh.SourceID = walitem.SourceID;
                                        oh.SupplierItemID = walitem.ID;
                                        models.OrderHistoryUpdate(oh, "ProposePrice", "MatchType", "MatchCount", "SourceID", "SupplierItemID");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                string msgItemID = (!string.IsNullOrEmpty(loopItemID)) ? "ItemID: " + loopItemID : "";
                string header = "UPCMatch RptNumber: " + rptNumber.ToString() + " " + msgItemID;
                string msg = dsutil.DSUtil.ErrMsg(header, exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "");
            }
        }

        private static List<TimesSold> FilterMatch(UserSettingsView settings, int rptNumber, int minSold, int daysBack, int? minPrice, int? maxPrice, bool? activeStatusOnly, bool? isSellerVariation, string itemID)
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
            return x.ToList();
        }

        private static async Task SearchEngineMatch(UserSettingsView settings, int rptNumber, int minSold, int daysBack, int? minPrice, int? maxPrice, bool? activeStatusOnly, bool? isSellerVariation, string itemID, double pctProfit, decimal wmShipping, decimal wmFreeShippingMin, double eBayPct, int imgLimit, string supplierTag)
        {
            string loopItemID = null;
            bool found = false;
            try
            {
                var mv = new ModelViewTimesSold();
                mv.TimesSoldRpt = FilterMatch(settings, rptNumber, minSold, daysBack, minPrice, maxPrice, activeStatusOnly, isSellerVariation, itemID);
                
                // Only search where MatchCount is null or does not equal 1
                mv.TimesSoldRpt = mv.TimesSoldRpt.Where(p => !p.MatchCount.HasValue || (p.MatchCount.HasValue && p.MatchCount.Value != 1)).ToList();

                foreach (var row in mv.TimesSoldRpt)
                {
                    loopItemID = row.ItemID;
                    if (loopItemID == "392388202275")
                    {
                        var stop = 999;
                    }
                    var walitem = new SupplierItem();
                    string descr = row.Description;
                    found = false;

                    // Search sections of description since most search engines only use first part of query anyway
                    for (int i = 0; i < 5; i++)
                    {
                        string section = GetDescrSection(descr, i);
                        if (!string.IsNullOrEmpty(section))
                        {
                            section = supplierTag + " " + section;
                            
                            //var links = dsutil.DSUtil.BingSearch(section);
                            var links = dsutil.DSUtil.GoogleSearchSelenium(section);

                            var validLinks = wallib.wmUtility.ValidURLs(links); // just get supplier links
                            if (validLinks.Count > 0)
                            {
                                // Collect valid supplier links from search engine result
                                foreach (string supplierURL in validLinks)
                                {
                                    walitem = await wallib.wmUtility.GetDetail(supplierURL, imgLimit);
                                    if (walitem != null)
                                    {
                                        // If can't get supplier pics, not much point in posting.
                                        // Can happen when not matching correctly on something like an eBook or giftcard where walmart
                                        // is not providing "standard" images. (error is logged in GetDetail()).
                                        if (!string.IsNullOrEmpty(walitem.SupplierPicURL))
                                        {
                                            if (!string.IsNullOrEmpty(walitem.ItemID) || !string.IsNullOrEmpty(walitem.UPC) || !string.IsNullOrEmpty(walitem.MPN))
                                            {
                                                found = true;
                                                walitem.Updated = DateTime.Now;
                                                models.SupplierItemUpdateByID(walitem,
                                                    "Updated",
                                                    "ItemURL",
                                                    "SoldAndShippedBySupplier",
                                                    "SupplierBrand",
                                                    "SupplierPrice",
                                                    "IsVariation",
                                                    "SupplierPicURL",
                                                    "IsFreightShipping");

                                                var oh = new OrderHistory();
                                                oh.ItemID = row.ItemID;
                                                oh.MatchCount = 1;
                                                oh.MatchType = 3;
                                                oh.SourceID = walitem.SourceID;
                                                oh.SupplierItemID = walitem.ID;
                                                if (walitem.SupplierPrice.HasValue)
                                                {
                                                    var p = wallib.wmUtility.wmNewPrice(walitem.SupplierPrice.Value, pctProfit, wmShipping, wmFreeShippingMin, eBayPct);
                                                    oh.ProposePrice = p.ProposePrice;
                                                    models.OrderHistoryUpdate(oh, "ProposePrice", "MatchType", "MatchCount", "SourceID", "SupplierItemID");
                                                }
                                                else
                                                {
                                                    models.OrderHistoryUpdate(oh, "MatchType", "MatchCount", "SourceID", "SupplierItemID");
                                                }
                                            }
                                        }
                                        if (found) break;
                                    }
                                }
                            }
                        }
                        if (found) break;
                    }
                }
            }
            catch (Exception exc)
            {
                string msgItemID = (!string.IsNullOrEmpty(loopItemID)) ? "ItemID: " + loopItemID : "";
                string header = "SearchEngineMatch RptNumber: " + rptNumber.ToString() + " " + msgItemID;
                string msg = dsutil.DSUtil.ErrMsg(header, exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "");
            }
        }

        /// <summary>
        /// Search part of description
        /// </summary>
        /// <param name="descr"></param>
        /// <param name="iteration"></param>
        /// <returns></returns>
        private static string GetDescrSection(string descr, int iteration)
        {
            string section = null;
            int skip = 150;
            int sectionLen = 250;
            try { 
                int pos = skip * iteration;
                if (pos < descr.Length)
                {
                    if (pos + sectionLen < descr.Length)
                    {
                        section = descr.Substring(pos, sectionLen);
                    }
                    else
                    {
                        section = descr.Substring(pos, descr.Length - pos);
                    }
                }
            }
             catch (Exception exc)
            {
                string header = "GetDescrSection";
                string msg = dsutil.DSUtil.ErrMsg(header, exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "");
            }
            return section;
        }
        /// <summary>
        /// This is where a SellerListing record is created.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="rptNumber"></param>
        /// <returns></returns>
        public static async Task<string> StoreToListing(UserSettingsView settings, int storeID)
        {
            string ret = "Copied 0 records.";
            int copiedRecords = 0;
            try
            {
                var recs = models.UpdateToListing.AsNoTracking().Where(p => p.StoreID == storeID && p.ToListing).ToList();
                foreach (var updateToList in recs)
                {
                    var oh = models.OrderHistory.AsNoTracking().Where(p => p.ItemID == updateToList.ItemID).SingleOrDefault();

                    var ohObj = models.OrderHistory.AsNoTracking().Include("ItemSpecifics").AsNoTracking().Where(p => p.ItemID == updateToList.ItemID).SingleOrDefault();

                    var UPC = ohObj.ItemSpecifics.Where(p => p.ItemName == "UPC").Select(q => q.ItemValue).FirstOrDefault();
                    var MPN = ohObj.ItemSpecifics.Where(p => p.ItemName == "MPN").Select(q => q.ItemValue).FirstOrDefault();
                    string foundResult = models.ProdIDExists(UPC, MPN, storeID);
                    if (foundResult == null)
                    {
                        var listing = new Listing();
                        listing.ItemID = ohObj.ItemID;
                        listing.ListingTitle = ohObj.Title;
                        if (ohObj.ProposePrice.HasValue)
                        {
                            listing.ListingPrice = ohObj.ProposePrice.Value;
                        }
                        var supplierItem = models.GetSupplierItem(oh.SupplierItemID.Value);
                        listing.SupplierID = supplierItem.ID;
                        listing.Profit = 0;
                        listing.ProfitMargin = 0;
                        listing.StoreID = storeID;
                        var descr = supplierItem.Description;
                        listing.Description = descr;
                        var upc = models.OrderHistoryItemSpecifics.AsNoTracking().Where(i => i.SellerItemID == ohObj.ItemID && i.ItemName == "UPC").SingleOrDefault();
                        if (upc != null)
                        {
                            listing.UPC = upc.ItemValue;
                        }

                        // MPN may have been collected twice - which one to use?  For now, pick first one.
                        var mpn = models.OrderHistoryItemSpecifics.AsNoTracking().Where(i => i.SellerItemID == ohObj.ItemID && i.ItemName == "MPN").FirstOrDefault();
                        if (mpn != null)
                        {
                            listing.MPN = mpn.ItemValue;
                        }
                        var si = await eBayUtility.ebayAPIs.GetSingleItem(settings, listing.ItemID);
                        listing.PrimaryCategoryID = si.PrimaryCategoryID;
                        listing.PrimaryCategoryName = si.PrimaryCategoryName;
                        
                        if (models.GetSellerListing(ohObj.ItemID) == null)
                        {
                            var sellerListing = new SellerListing();
                            sellerListing.ItemID = ohObj.ItemID;
                            sellerListing.Title = ohObj.Title;
                            sellerListing.Seller = si.Seller;
                            sellerListing.PrimaryCategoryID = si.PrimaryCategoryID;
                            sellerListing.PrimaryCategoryName = si.PrimaryCategoryName;
                            sellerListing.Description = si.Description;
                            sellerListing.ListingStatus = si.ListingStatus;
                            sellerListing.EbayURL = si.EbayURL;
                            sellerListing.PictureURL = si.PictureURL;
                            sellerListing.SellerPrice = si.SellerPrice;
                            sellerListing.Updated = DateTime.Now;
                            sellerListing.ItemSpecifics = dsmodels.DataModelsDB.CopyFromOrderHistory(ohObj.ItemSpecifics);
                            listing.SellerListing = sellerListing;
                        }
                        await models.ListingSaveAsync(settings, listing);

                        var obj = new UpdateToListing() { StoreID = storeID, ItemID = ohObj.ItemID };
                        await models.UpdateToListingRemove(obj);
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
