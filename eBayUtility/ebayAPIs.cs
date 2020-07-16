/*
 * eBay Get functions and Finding methods.
 * 
 * 
 */

using dsmodels;
using dsutil;
using eBay.Service.Call;
using eBay.Service.Core.Sdk;
using eBay.Service.Core.Soap;
using eBayUtility.WebReference;
using eBayUtility.WebReference1;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;
using Utility;

namespace eBayUtility
{
    // TokenStatusType is an eBay type that has a property called Status which is an enum
    // Here, another property has been added to show the name of the enum
    public class TokenStatusTypeCustom : TokenStatusType
    {
        public string StatusStr { get; set; }
    }

    public class ebayAPIs
    {
        readonly static string _logfile = "log.txt";
        //dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
        static DataModelsDB models = new DataModelsDB();

        /// <summary>
        /// GetSellerTransactions
        /// https://developer.ebay.com/DevZone/XML/Docs/Reference/ebay/GetSellerTransactions.html
        /// </summary>
        /// <param name="orderID">19-04026-11927</param>
        public static GetOrdersResponse GetOrders(IUserSettingsView settings, string orderID, out string msg)
        {
            msg = null;
            var eBayOrder = new GetOrdersResponse();
            ApiContext context = new ApiContext();
            try
            {
                string token = models.GetToken(settings);
                context.ApiCredential.eBayToken = token;

                // set the server url
                string endpoint = "https://api.ebay.com/wsapi";
                context.SoapApiServerUrl = endpoint;

                GetOrdersCall call = new GetOrdersCall(context);
                call.DetailLevelList = new DetailLevelCodeTypeCollection();
                call.DetailLevelList.Add(DetailLevelCodeType.ReturnAll);
                call.OrderIDList = new StringCollection();
                call.OrderIDList.Add(orderID);
                call.Execute();

                var r = call.ApiResponse.OrderArray;
                eBayOrder.BuyerHandle = r[0].BuyerUserID;     // customer eBay handle
                eBayOrder.DatePurchased = r[0].PaidTime;
                var ShippingAddress = r[0].ShippingAddress;
                // Name
                eBayOrder.Buyer = ShippingAddress.Name;
                // PostalCode
                // StateOrProvince
                // Street1
                // Phone
                // CityName
                var SubTotal = r[0].Subtotal;
                var Total = r[0].Total;
                eBayOrder.BuyerPaid = (decimal)r[0].AmountPaid.Value;
                eBayOrder.BuyerState = ShippingAddress.StateOrProvince;

                // orderID is returned as a hyphenated string like:
                // 223707436249-2329703153012
                // first number is the itemID
                var OrderID = r[0].OrderID;

                var a = call.ApiResponse.Ack;
            }
            catch (Exception exc)
            {
                msg = dsutil.DSUtil.ErrMsg("GetOrders", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
            }
            return eBayOrder;
        }

        public static List<SalesOrder> GetOrdersByDate(IUserSettingsView settings, string itemID, DateTime fromDate, DateTime toDate, double finalValueFeePct)
        {
            var orders = ebayAPIs.GetOrdersByDate(settings, fromDate, toDate, finalValueFeePct, "");
            var eBayOrders = new List<SalesOrder>();
            foreach (var order in orders)
            {
                if (order.ListedItemID == itemID)
                {
                    eBayOrders.Add(order);
                }
            }
            return eBayOrders;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="fromDate"></param>
        /// <param name="toDate"></param>
        /// <param name="finalValueFeePct"></param>
        /// <param name="orderStatus">Pass 'Cancelled' or 'RETURN' or 'ReturnRequestPending'</param>
        /// <returns></returns>
        public static List<SalesOrder> GetOrdersByDate(IUserSettingsView settings, DateTime fromDate, DateTime toDate, double finalValueFeePct, string orderStatus)
        {
            var eBayOrders = new List<SalesOrder>();
            ApiContext context = new ApiContext();
            try
            {
                string token = models.GetToken(settings);
                context.ApiCredential.eBayToken = token;

                // set the server url
                string endpoint = "https://api.ebay.com/wsapi";
                context.SoapApiServerUrl = endpoint;

                GetOrdersCall call = new GetOrdersCall(context);
                call.DetailLevelList = new DetailLevelCodeTypeCollection();
                call.DetailLevelList.Add(DetailLevelCodeType.ReturnAll);
                call.CreateTimeFrom = fromDate;
                call.CreateTimeTo = toDate;
                call.Execute();

                var a = call.ApiResponse.Ack;
                foreach (var r in call.ApiResponse.OrderArray.ToArray())
                {
                    var response = new SalesOrder();
                    if (r.TransactionArray.Count == 1)
                    {                                // i'm not expecting a value other than 1 here
                        response.Qty = r.TransactionArray[0].QuantityPurchased;
                        response.DatePurchased = r.TransactionArray[0].CreatedDate;     // there are various dates to use - let's see how close this one is
                                                                                        //r.TransactionArray[0].Taxes.TotalTaxAmount;
                        response.SalesTax = (decimal)r.TransactionArray[0].Taxes.TotalTaxAmount.Value;

                        if (r.TransactionArray[0].ShippingDetails.ShipmentTrackingDetails.Count > 0)
                        {
                            response.TrackingNumber = r.TransactionArray[0].ShippingDetails.ShipmentTrackingDetails[0].ShipmentTrackingNumber;
                        }

                        var c = r.TransactionArray.Count;
                        //var x = r.TransactionArray[0].MonetaryDetails.Refunds.Refund[0].RefundAmount.Value;
                        //var y = r.TransactionArray[0].RefundAmount.Value;
                        if (r.TransactionArray[0].ActualShippingCost != null)
                        {
                            response.ShippingCost = (decimal)r.TransactionArray[0].ActualShippingCost.Value;
                        }
                    }
                    //var x = r.RefundAmount.Value;
                    response.BuyerHandle = r.BuyerUserID;     // customer eBay handle
                    //if (response.BuyerHandle == "sunnydawn65")
                    //{
                    //    int stop = 99;
                    //}
                    var rs = r.TransactionArray[0].Status.ReturnStatus;
                    string rsname = Enum.GetName(typeof(ReturnStatusCodeType), rs);
                    response.ReturnStatus = rsname;
                  
                    response.DatePurchased = r.PaidTime;
                    var ShippingAddress = r.ShippingAddress;
                    // Name
                    response.Buyer = ShippingAddress.Name;
                    // PostalCode
                    // StateOrProvince
                    // Street1
                    // Phone
                    // CityName
                    var SubTotal = r.Subtotal;
                    response.SubTotal = (decimal)SubTotal.Value;
                    var Total = r.Total;
                    response.Total = (decimal)Total.Value;
                    response.BuyerPaid = (decimal)r.AmountPaid.Value;
                    response.BuyerState = ShippingAddress.StateOrProvince;
                    response.FinalValueFee = (response.SubTotal + response.ShippingCost) * (decimal)finalValueFeePct;
                    response.PayPalFee = (response.Total * 0.029m) + 0.30m;
                    response.OrderID = r.OrderID;

                    // orderID is returned as a hyphenated string like:
                    // 223707436249-2329703153012
                    // first number is the itemID
                    var orderID = r.OrderID;
                    response.ListedItemID = GetItemIDFromGetOrders(orderID);
                    response.OrderID = GetOrderIDFromGetOrders(orderID);
                    response.OrderStatus = r.OrderStatus.ToString();

                    string csname = Enum.GetName(typeof(CancelStatusCodeType), r.CancelStatus);
                    response.CancelStatus = csname;

                    if (!string.IsNullOrEmpty(orderStatus))
                    {
                        if (orderStatus == "RETURN")
                        {
                            if (rsname != "NotApplicable")
                            {
                                eBayOrders.Add(response);
                            }
                        }
                        else if (orderStatus == "ReturnRequestPending")
                        {
                            if (rsname == "ReturnRequestPending")
                            {
                                eBayOrders.Add(response);
                            }
                        }
                        else
                        {
                            if (orderStatus == response.OrderStatus)
                            {
                                eBayOrders.Add(response);
                            }
                        }
                    }
                    else
                    {
                        eBayOrders.Add(response);
                    }
                }
                return eBayOrders;
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetOrdersByDate", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                throw;
            }
        }
        protected static string GetItemIDFromGetOrders(string orderID)
        {
            int pos = orderID.IndexOf("-");
            string ret = orderID.Substring(0, pos);
            return ret;
        }
        protected static string GetOrderIDFromGetOrders(string orderID)
        {
            int pos = orderID.IndexOf("-");
            string ret = orderID.Substring(pos + 1, orderID.Length - (pos + 1));
            return ret;
        }
        public static void GetOrderTransactions(UserSettingsView settings, string itemID)
        {
            //create the context
            ApiContext context = new ApiContext();

            string token = models.GetToken(settings);
            context.ApiCredential.eBayToken = token;

            // set the server url
            string endpoint = "https://api.ebay.com/wsapi";
            context.SoapApiServerUrl = endpoint;

            //context.ApiLogManager = newApiLogManager();
            //context.ApiLogManager.ApiLoggerList.Add(new eBay.Service.Util.FileLogger("Messages.log", true, true, true));
            //context.ApiLogManager.EnableLogging = true;

            // Set the version
            context.Version = "581";

            GetOrderTransactionsCall call = new GetOrderTransactionsCall(context);
            call.ItemTransactionIDArrayList = new ItemTransactionIDTypeCollection();

            //add as many ItemID TransactionID objects as required
            ItemTransactionIDType itemTrans1 = new ItemTransactionIDType();
            itemTrans1.ItemID = itemID;
            itemTrans1.TransactionID = "0";
            call.ItemTransactionIDArrayList.Add(itemTrans1);

            //ItemTransactionIDType itemTrans2 = new ItemTransactionIDType();
            //itemTrans2.ItemID = "110035634592";
            //itemTrans2.TransactionID = "1234567890";
            //call.ItemTransactionIDArrayList.Add(itemTrans2);

            //set DetailLevel to ReturnAll to get the ExternalTransactionID call.DetailLevelList.Add(DetailLevelCodeType.ReturnAll);

            call.Execute();

            //Process the response
            foreach (OrderType order in call.OrderList)
            {
                //some sample information retrieved
                string buyerUserID = order.BuyerUserID;
                if (order.ShippingDetails.SalesTax != null)
                {
                    string salesTaxState = order.ShippingDetails.SalesTax.SalesTaxState;
                    float salesTaxPercent = order.ShippingDetails.SalesTax.SalesTaxPercent;
                    double salesTaxAmt = order.ShippingDetails.SalesTax.SalesTaxAmount.Value;
                }

                TransactionType transaction = order.TransactionArray[0];
                string buyerEmail = transaction.Buyer.Email;
                double amtPaid = transaction.AmountPaid.Value;
            }
        }
        /// <summary>
        /// Gives you info about eBay as a whole
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static string GetebayDetails(UserSettingsView settings)
        {
            string unavailable = null;
            try
            {
                ApiContext context = new ApiContext();
                string token = settings.Token;
                context.ApiCredential.eBayToken = token;
                context.SoapApiServerUrl = "https://api.ebay.com/wsapi";
                // set the version 
                context.Version = "865";
                context.Site = eBay.Service.Core.Soap.SiteCodeType.US;

                GeteBayDetailsCall request = new GeteBayDetailsCall(context);
                request.DetailNameList = new DetailNameCodeTypeCollection();
                //request.DetailNameList.Add(DetailNameCodeType.ProductDetails);
                //request.DetailNameList.Add(DetailNameCodeType.ReturnPolicyDetails);

                // No, this gives you all of eBay's shipping options
                request.DetailNameList.Add(DetailNameCodeType.ShippingServiceDetails);
                GeteBayDetailsResponseType response = new GeteBayDetailsResponseType();
                request.Execute();
                response = request.ApiResponse;
                if (response.Ack == eBay.Service.Core.Soap.AckCodeType.Success)
                {
                    //unavailable = response.ProductDetails.ProductIdentifierUnavailableText;


                    var s = response.ShippingServiceDetails.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return unavailable;
        }

        // findCompletedItems
        // this is a member of the Finding API.  My understanding is that the .NET SDK only supports the Trading API
        public static async Task FindItemsAsync_havenotbeenusing()
        {
            string uri = "http://svcs.ebay.com/services/search/FindingService/v1?SECURITY-APPNAME=KevinWei-Test-PRD-25d7a0307-a9330e4a&OPERATION-NAME=findCompletedItems&SERVICE-VERSION=1.13.0&GLOBAL-ID=EBAY-US&RESPONSE-DATA-FORMAT=JSON&REST-PAYLOAD&itemFilter(0).name=Seller&itemFilter(0).paramName=name&itemFilter(0).paramValue=Seller&itemFilter(0).value(0)=**justforyou**&itemFilter(1).name=SoldItemsOnly&itemFilter(1).value(0)=true";

            // ... Use HttpClient.
            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(uri))
            using (HttpContent content = response.Content)
            {
                // ... Read the string.
                string result = await content.ReadAsStringAsync();

                // ... Display the result.
                if (result != null &&
                    result.Length >= 50)
                {
                    Console.WriteLine(result);
                }
            }
        }

        /// <summary>
        /// Get all items in the store
        /// https://ebaydts.com/eBayKBDetails?KBid=475
        /// a variety of this is to use findCompletedItems
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="errMsg"></param>
        /// <returns></returns>
        public static ItemTypeCollection GetSellerList(UserSettingsView settings, out string errMsg)
        {
            // TODO: Add code to start application here
            //
            // first, set up the ApiContext object
            errMsg = null;
            ApiContext oContext = new ApiContext();

            // set the dev,app,cert information
            oContext.ApiCredential.ApiAccount.Application = settings.AppID;
            oContext.ApiCredential.ApiAccount.Developer = settings.DevID;
            oContext.ApiCredential.ApiAccount.Certificate = settings.CertID;

            // set the AuthToken
            oContext.ApiCredential.eBayToken = settings.Token;

            oContext.SoapApiServerUrl = "https://api.ebay.com/wsapi";

            // set the Site of the Context
            oContext.Site = eBay.Service.Core.Soap.SiteCodeType.US;

            // very important, let's setup the logging
            /*
            ApiLogManager oLogManager = new ApiLogManager();
            oLogManager.ApiLoggerList.Add(new eBay.Service.Util.FileLogger("logebay.txt", false, false, false));
            oLogManager.EnableLogging = true;
            oContext.ApiLogManager = oLogManager;
            */

            // the WSDL Version used for this SDK build
            oContext.Version = "459";

            // set the CallRetry properties
            CallRetry oCallRetry = new CallRetry();
            // set the delay between each retry to 1 millisecond
            oCallRetry.DelayTime = 1;
            // set the maximum number of retries
            oCallRetry.MaximumRetries = 3;
            // set the error codes on which to retry
            eBay.Service.Core.Soap.StringCollection oErrorCodes = new eBay.Service.Core.Soap.StringCollection();
            oErrorCodes.Add("10007"); // Internal error to the application ... general error
            oErrorCodes.Add("2"); // unsupported verb error
            oErrorCodes.Add("251"); // eBay Structured Exception ... general error
            oCallRetry.TriggerErrorCodes = oErrorCodes;
            // set the exception types on which to retry
            TypeCollection oExceptions = new TypeCollection();
            oExceptions.Add(typeof(System.Net.ProtocolViolationException));
            // the "Client found response content type of 'text/plain'" exception is of type SdkException, so let's add that to the list
            oExceptions.Add(typeof(SdkException));
            oCallRetry.TriggerExceptions = oExceptions;

            // set CallRetry back to ApiContext
            oContext.CallRetry = oCallRetry;

            // set the timeout to 2 minutes
            oContext.Timeout = 120000;

            GetSellerListCall oGetSellerListCall = new GetSellerListCall(oContext);

            // set the Version used in the call
            oGetSellerListCall.Version = oContext.Version;

            // set the Site of the call
            oGetSellerListCall.Site = oContext.Site;

            // enable the compression feature
            oGetSellerListCall.EnableCompression = true;

            // use GranularityLevel of Fine
            oGetSellerListCall.GranularityLevel = GranularityLevelCodeType.Fine;

            // get the first page, 200 items per page
            PaginationType oPagination = new PaginationType();
            oPagination.EntriesPerPage = 100;
            oPagination.EntriesPerPageSpecified = true;
            oPagination.PageNumber = 1;
            oPagination.PageNumberSpecified = true;
            oGetSellerListCall.Pagination = oPagination;

            // ask for all items that are ending in the future (active items)
            oGetSellerListCall.EndTimeFilter = new TimeFilter(DateTime.Now, DateTime.Now.AddMonths(3));
            oGetSellerListCall.UserID = settings.StoreName;

            // return items that end soonest first
            oGetSellerListCall.Sort = 2;
            // see http://developer.ebay.com/DevZone/SOAP/docs/WSDL/xsd/1/element/1597.htm for Sort documentation

            var oItems = new ItemTypeCollection();
            oItems = null;
            try
            {
                oItems = oGetSellerListCall.GetSellerList();
            }
            catch (ApiException oApiEx)
            {
                // process exception ... pass to caller, implement retry logic here or in caller, whatever you want to do
                Console.WriteLine(oApiEx.Message);
                errMsg = oApiEx.Message;
            }
            catch (SdkException oSdkEx)
            {
                // process exception ... pass to caller, implement retry logic here or in caller, whatever you want to do
                Console.WriteLine(oSdkEx.Message);
                errMsg = oSdkEx.Message;
            }
            catch (Exception oEx)
            {
                // process exception ... pass to caller, implement retry logic here or in caller, whatever you want to do
                Console.WriteLine(oEx.Message);
                errMsg = oEx.Message;
            }
            return oItems;
        }

        // https://ebaydts.com/eBayKBDetails?KBid=1937
        //
        // also look at GetOrderTransactions()
        //
        // GetSellerTransactions
        // https://developer.ebay.com/DevZone/XML/Docs/Reference/ebay/GetSellerTransactions.html
        public static TransactionTypeCollection GetItemTransactions(UserSettingsView settings, string itemID, DateTime ModTimeFrom, DateTime ModTimeTo)
        {
            dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();

            // var profile = db.GetUserProfile(user.Id);
            ApiContext oContext = new ApiContext();

            //set the dev,app,cert information
            oContext.ApiCredential.ApiAccount.Developer = settings.DevID;
            oContext.ApiCredential.ApiAccount.Application = settings.AppID;
            oContext.ApiCredential.ApiAccount.Certificate = settings.CertID;
            oContext.ApiCredential.eBayToken = db.GetToken(settings);

            oContext.SoapApiServerUrl = "https://api.ebay.com/wsapi";

            //set the Site of the Context
            oContext.Site = eBay.Service.Core.Soap.SiteCodeType.US;

            //the WSDL Version used for this SDK build
            oContext.Version = "817";

            //very important, let's setup the logging
            ApiLogManager oLogManager = new ApiLogManager();
            oLogManager.ApiLoggerList.Add(new eBay.Service.Util.FileLogger("logebay.txt", false, false, false));
            oLogManager.EnableLogging = true;
            oContext.ApiLogManager = oLogManager;

            GetItemTransactionsCall oGetItemTransactionsCall = new GetItemTransactionsCall(oContext);

            //' set the Version used in the call
            oGetItemTransactionsCall.Version = oContext.Version;

            //' set the Site of the call
            oGetItemTransactionsCall.Site = oContext.Site;

            //' enable the compression feature
            oGetItemTransactionsCall.EnableCompression = true;

            DateTime CreateTimeFromPrev;

            //ModTimeTo set to the current time
            //ModTimeTo = DateTime.Now.ToUniversalTime();

            //ts1 is 15 mins
            //TimeSpan ts1 = new TimeSpan(9000000000);
            //CreateTimeFromPrev = ModTimeTo.AddDays(-30);

            //Set the ModTimeFrom the last time you made the call minus 2 minutes
            //ModTimeFrom = CreateTimeFromPrev;

            //set ItemID and <DetailLevel>ReturnAll<DetailLevel>
            oGetItemTransactionsCall.ItemID = itemID;
            oGetItemTransactionsCall.DetailLevelList.Add(DetailLevelCodeType.ReturnAll);

            var r = oGetItemTransactionsCall.GetItemTransactions(itemID, ModTimeFrom, ModTimeTo);
            var b = oGetItemTransactionsCall.HasError;

            return r;
        }

        public static ApiAccessRuleTypeCollection GetAPIStatus(UserSettingsView settings)
        {
            try
            {
                dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
                // var setting = db.UserSettings.Find(user.Id, 1);
                // var profile = db.GetUserProfile(user.Id);
                ApiContext oContext = new ApiContext();

                //set the dev,app,cert information
                oContext.ApiCredential.ApiAccount.Developer = settings.DevID;
                oContext.ApiCredential.ApiAccount.Application = settings.AppID;
                oContext.ApiCredential.ApiAccount.Certificate = settings.CertID;
                oContext.ApiCredential.eBayToken = settings.Token;

                oContext.SoapApiServerUrl = "https://api.ebay.com/wsapi";

                //set the Site of the Context
                oContext.Site = eBay.Service.Core.Soap.SiteCodeType.US;

                //the WSDL Version used for this SDK build
                oContext.Version = "817";
                GetApiAccessRulesCall oGetApiAccessRulesCall = new GetApiAccessRulesCall(oContext);

                //' set the Version used in the call
                oGetApiAccessRulesCall.Version = oContext.Version;

                //' set the Site of the call
                oGetApiAccessRulesCall.Site = oContext.Site;

                //' enable the compression feature
                oGetApiAccessRulesCall.EnableCompression = true;
                var r = oGetApiAccessRulesCall.GetApiAccessRules();
                return r;
            }
            catch (Exception ex)
            {
                string s = ex.Message;
                return null;
            }
        }

        // note below that GetApiAccessRules returns a collection but first item is CallName, ApplicationAggregate, which returns all
        public static long GetTradingAPIUsage(IUserSettingsView settings)
        {
            try
            {
                dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
                // var setting = db.UserSettings.Find(user.Id, 1);
                // var profile = db.GetUserProfile(user.Id);
                ApiContext oContext = new ApiContext();

                //set the dev,app,cert information
                oContext.ApiCredential.ApiAccount.Developer = settings.DevID;
                oContext.ApiCredential.ApiAccount.Application = settings.AppID;
                oContext.ApiCredential.ApiAccount.Certificate = settings.CertID;
                oContext.ApiCredential.eBayToken = settings.Token;

                oContext.SoapApiServerUrl = "https://api.ebay.com/wsapi";

                //set the Site of the Context
                oContext.Site = eBay.Service.Core.Soap.SiteCodeType.US;

                //the WSDL Version used for this SDK build
                oContext.Version = "817";
                GetApiAccessRulesCall oGetApiAccessRulesCall = new GetApiAccessRulesCall(oContext);
                GetStoreCall oGetStore = new GetStoreCall(oContext);
                oGetStore.CategoryStructureOnly = true;
                oGetStore.Execute();
                var x = oGetStore.Store.Name;

                //' set the Version used in the call
                oGetApiAccessRulesCall.Version = oContext.Version;

                //' set the Site of the call
                oGetApiAccessRulesCall.Site = oContext.Site;

                //' enable the compression feature
                oGetApiAccessRulesCall.EnableCompression = true;

                var r = oGetApiAccessRulesCall.GetApiAccessRules();
                var i = r[0].DailyUsage;
                return i;
            }
            catch (Exception ex)
            {
                string s = ex.Message;
                return -1;
            }
        }

        /// <summary>
        /// https://stackoverflow.com/questions/33095566/i-am-trying-to-check-for-expired-token-for-ebay-sdk-with-getclientalertsauthtoke
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static TokenStatusTypeCustom GetTokenStatus(UserSettingsView settings)
        {
            try
            {
                dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
                ApiContext oContext = new ApiContext();

                //set the dev,app,cert information
                oContext.ApiCredential.ApiAccount.Developer = settings.DevID;
                oContext.ApiCredential.ApiAccount.Application = settings.AppID;
                oContext.ApiCredential.ApiAccount.Certificate = settings.CertID;
                oContext.ApiCredential.eBayToken = settings.Token;

                oContext.SoapApiServerUrl = "https://api.ebay.com/wsapi";

                //set the Site of the Context
                oContext.Site = eBay.Service.Core.Soap.SiteCodeType.US;

                //the WSDL Version used for this SDK build
                oContext.Version = "817";
                GetTokenStatusCall oGetTokenStatusCall = new GetTokenStatusCall(oContext);

                //' set the Version used in the call
                oGetTokenStatusCall.Version = oContext.Version;

                //' set the Site of the call
                oGetTokenStatusCall.Site = oContext.Site;

                //' enable the compression feature
                oGetTokenStatusCall.EnableCompression = true;

                var r = oGetTokenStatusCall.GetTokenStatus();
                TokenStatusTypeCustom final = new TokenStatusTypeCustom();
                final.StatusStr = r.Status.ToString();
                final.ExpirationTime = r.ExpirationTime;

                return final;
            }
            catch (Exception ex)
            {
                string s = ex.Message;
                return null;
            }
        }

        // https://ebaydts.com/eBayKBDetails?KBid=1987
        //
        // 192369073559
        protected static void GetItem(string itemId)
        {
            ApiContext oContext = new ApiContext();

            //set the endpoint (sandbox) use https://api.ebay.com/wsapi for production
            oContext.SoapApiServerUrl = "https://api.ebay.com/wsapi";

            //set the Site of the Context
            oContext.Site = eBay.Service.Core.Soap.SiteCodeType.US;

            //the WSDL Version used for this SDK build
            oContext.Version = "735";

            //very important, let's setup the logging
            ApiLogManager oLogManager = new ApiLogManager();
            oLogManager.ApiLoggerList.Add(new eBay.Service.Util.FileLogger("logebay.txt", false, false, false));
            oLogManager.EnableLogging = true;
            oContext.ApiLogManager = oLogManager;

            GetItemCall oGetItemCall = new GetItemCall(oContext);

            //' set the Version used in the call
            oGetItemCall.Version = oContext.Version;

            //' set the Site of the call
            oGetItemCall.Site = oContext.Site;

            //' enable the compression feature
            oGetItemCall.EnableCompression = true;

            oGetItemCall.DetailLevelList.Add(DetailLevelCodeType.ReturnAll);

            oGetItemCall.ItemID = itemId;

            var r = oGetItemCall.GetItem(oGetItemCall.ItemID);
            var sold = r.SellingStatus.QuantitySold;
            var pic = r.PictureDetails.PictureURL;
        }

        /// <summary>
        /// Get seller's shipping information.
        /// 10.07.2019 NOTE: While this works, not using it since overkill on every call.
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="appid"></param>
        /// <returns></returns>
        public static async Task<ShippingCostSummary> GetShippingCosts(string itemId, string appid)
        {
            try
            {
                var shippingCost = new ShippingCostSummary();
                Shopping svc = new Shopping();
                // set the URL and it's parameters
                svc.Url = string.Format("http://open.api.ebay.com/shopping?callname=GetShippingCosts&responseencoding=XML&appid={0}&siteid=0&version=517&ItemID={1}&DestinationCountryCode=US&DestinationPostalCode=95128&IncludeDetails=true&QuantitySold=1", appid, itemId);
                string uri = svc.Url;
                string errMsg;
                using (HttpClient httpClient = new HttpClient())
                {
                    string s = await httpClient.GetStringAsync(uri);
                    s = s.Replace("\"", "'");
                    string output = s.Replace(" xmlns='urn:ebay:apis:eBLBaseComponents'", string.Empty);

                    errMsg = GetSingleItemError(output);
                    if (!string.IsNullOrEmpty(errMsg))
                    {
                        throw new Exception(errMsg);
                    }
                    XElement root = XElement.Parse(output);
                    var qryRecords = from record in root.Elements("ShippingCostSummary")
                                     select record;

                    if (qryRecords.Count() > 0)
                    {
                        var r = (from r2 in qryRecords
                                 select new
                                 {
                                     ShippingServiceName = r2.Element("ShippingServiceName"),
                                     ShippingServiceCost = r2.Element("ShippingServiceCost")
                                 }).Single();
                        shippingCost.ShippingServiceName = r.ShippingServiceName.Value;
                        shippingCost.ShippingServiceCost = r.ShippingServiceCost.Value;
                        return shippingCost;
                    }
                }
                return null;
            }
            catch (Exception exc)
            {
                return null;
            }
        }

        // not working - trying to get my store's shipping policy
        public static async Task GetShippingPolicy(UserSettingsView settings)
        {
            try
            {
                Shopping svc = new Shopping();
                // set the URL and it's parameters
                svc.Url = string.Format("https://svcs.ebay.com/services/selling/v1/SellerProfilesManagementService?X-EBAY-SOA-OPERATION-NAME=getSellerProfiles&X-EBAY-SOA-SERVICE-NAME=SellerProfilesManagementService&X-EBAY-SOA-SERVICE-VERSION=1.0.0&X-EBAY-SOA-SECURITY-TOKEN={0}&X-EBAY-SOA-RESPONSE-DATA-FORMAT=XML&REST-PAYLOAD&includeDetails=true", settings.Token);
                string uri = svc.Url;
                string errMsg;
                using (HttpClient httpClient = new HttpClient())
                {
                    string s = await httpClient.GetStringAsync(uri);
                 
                }
            }
            catch (Exception exc)
            {
                string msg = exc.Message;
            }
        }
      
        // Purpose of GetSingleItem is to fetch properties such as a listing's description and photos
        // it is used when performing an auto-listing
        public static async Task<SellerListing> GetSingleItem(IUserSettingsView settings, string itemID, bool includeItemSpecifics)
        {
            string errMsg = null;
            StringReader sr;
            string output;
            string variationName = null;
            var sellerListing = new SellerListing();
            const string notfound = "Invalid item ID.";

            try
            {
                DataModelsDB db = new DataModelsDB();

                Shopping svc = new Shopping();
                // set the URL and it's parameters

                // docs for IncludeSelector
                // https://developer.ebay.com/devzone/shopping/docs/callref/getsingleitem.html
                if (includeItemSpecifics)
                {
                    svc.Url = string.Format("http://open.api.ebay.com/shopping?callname=GetSingleItem&IncludeSelector=Details,TextDescription,ItemSpecifics,Variations&appid={0}&version=515&ItemID={1}", settings.AppID, itemID);
                }
                else
                {
                    svc.Url = string.Format("http://open.api.ebay.com/shopping?callname=GetSingleItem&IncludeSelector=Details,TextDescription,Variations&appid={0}&version=515&ItemID={1}", settings.AppID, itemID);
                }
                // create a new request type
                GetSingleItemRequestType request = new GetSingleItemRequestType();
                // put in your own item number
                //request.ItemID = itemId;
                // we will request Details
                // for IncludeSelector reference see
                // http://developer.ebay.com/DevZone/shopping/docs/CallRef/GetSingleItem.html#detailControls
                //request.IncludeSelector = "Details";
                //request.IncludeSelector = "Details,Description,TextDescription";
                // create a new response type
                GetSingleItemResponseType response = new GetSingleItemResponseType();

                string uri = svc.Url;
                using (HttpClient httpClient = new HttpClient())
                {
                    string s = await httpClient.GetStringAsync(uri);
                    s = s.Replace("\"", "'");
                    output = s.Replace(" xmlns='urn:ebay:apis:eBLBaseComponents'", string.Empty);

                    errMsg = GetSingleItemError(output);
                    if (errMsg != notfound)
                    {
                        if (!string.IsNullOrEmpty(errMsg))
                        {
                            throw new Exception(errMsg);
                        }
                        XElement root = XElement.Parse(output);
                        var qryRecords = from record in root.Elements("Item")
                                         select record;
                        if (qryRecords.Count() == 0)
                        {
                            return null;
                        }
                        var r = (from r2 in qryRecords
                                 select new
                                 {
                                     Description = r2.Element("Description"),
                                     Title = r2.Element("Title"),
                                     Price = r2.Element("ConvertedCurrentPrice"),
                                     ListingUrl = r2.Element("ViewItemURLForNaturalSearch"),
                                     PrimaryCategoryID = r2.Element("PrimaryCategoryID"),
                                     PrimaryCategoryName = r2.Element("PrimaryCategoryName").ElementValueNull(),
                                     Quantity = r2.Element("Quantity"),
                                     QuantitySold = r2.Element("QuantitySold"),
                                     ListingStatus = r2.Element("ListingStatus"),
                                     Seller = r2.Element("Seller").Element("UserID"),
                                     Shipping = r2.Element("ShippingCostSummary").ElementValueNull()
                                 }).Single();

                        // 10.10.2019 had case where no pictures being returned for a seller's listing - idk
                        // again, probably has something to do with variation listing?
                        // https://www.ebay.com/itm/XXL-Folding-Padded-Director-Chair-Heavy-Duty-W-Side-Table-Drink-Holder-500-Lb-/352687221867?var=
                        //
                        var list = qryRecords.Elements("PictureURL")
                                .Select(element => element.Value)
                                .ToArray();
                        if (list.Count() == 0)
                        {
                            list = qryRecords.Elements("Variations").Elements("Pictures").Elements("VariationSpecificPictureSet").Elements("PictureURL")
                                .Select(element => element.Value)
                                .ToArray();
                        }

                        #region Generate list of Variation objects
                        var variations = qryRecords.Elements("Variations").Elements("Variation")
                                .ToArray();
                        XmlSerializer serializer = new XmlSerializer(typeof(Variation));
                        var variationList = new List<Variation>();
                        foreach (var v in variations)
                        {
                            var variation = (Variation)serializer.Deserialize(v.CreateReader());
                            variationName = variation.VariationSpecifics.NameValueList.Name;
                            variationList.Add(variation);
                        }

                        #endregion

                        var specifics = (from r3 in qryRecords.Elements("ItemSpecifics").Elements("NameValueList")
                                         select new
                                         {
                                             Name = r3.Element("Name").Value,
                                             Value = r3.Element("Value").Value
                                         }).ToArray();

                        var itemSpecifics = new List<SellerListingItemSpecific>();
                        foreach (var i in specifics)
                        {
                            string n = i.Name;
                            string v = i.Value;
                            var specific = new SellerListingItemSpecific();
                            specific.SellerItemID = itemID;
                            specific.ItemName = n;
                            specific.ItemValue = v;
                            itemSpecifics.Add(specific);
                        }
                        var a = r.Shipping;

                        if (!string.IsNullOrEmpty(variationName))
                        {
                            sellerListing.Variation = true;
                        }
                        sellerListing.VariationName = variationName;
                        sellerListing.ItemSpecifics = itemSpecifics.ToList();
                        sellerListing.Variations = variationList;
                        /*
                         * 10.07.2019
                         * Good to know how to do this but not necessary since can mostly just look up the seller and see what his
                         * shipping policy is.  Don't need to exhaust API calls on this.
                        var shippingCost = await GetShippingCosts(itemId, appid);
                        si.ShippingServiceCost = shippingCost.ShippingServiceCost;
                        si.ShippingServiceName = shippingCost.ShippingServiceName;
                        */
                        sellerListing.ItemID = itemID;
                        sellerListing.PictureURL = DSUtil.ListToDelimited(list, ';');
                        sellerListing.Title = r.Title.Value;
                        sellerListing.Description = r.Description.Value;
                        sellerListing.SellerPrice = Convert.ToDecimal(r.Price.Value);
                        sellerListing.EbayURL = r.ListingUrl.Value;
                        sellerListing.PrimaryCategoryID = r.PrimaryCategoryID.Value;
                        sellerListing.PrimaryCategoryName = r.PrimaryCategoryName;
                        int x1 = Convert.ToInt32(r.Quantity.Value);
                        int x2 = Convert.ToInt32(r.QuantitySold.Value);
                        sellerListing.Qty = x1 - x2;   // available qty; https://forums.developer.ebay.com/questions/11293/how-to-get-item-quantity-available.html
                        sellerListing.ListingStatus = r.ListingStatus.Value;
                        sellerListing.Seller = r.Seller.Value;
                    }
                    else
                    {
                        sellerListing = null;
                    }
                }
            }
            catch (Exception exc)
            {
                string msg = "itemid: " + itemID.ToString() + " GetSingleItem " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                throw;
            }
            return sellerListing;
        }

        protected static string GetSingleItemError(string output)
        {
            string errMsg = null;
            try
            {
                var elem = XElement.Parse(output);
                string xmlErr = null;
                if (elem.Element("Errors") != null)
                {
                    xmlErr = (string)elem.Element("Errors").Element("ShortMessage");
                    if (!string.IsNullOrEmpty(xmlErr))
                    {
                        errMsg = xmlErr;
                    }
                }
            }
            catch { }
            return errMsg;
        }

        // Was being developed when trying to get details of an item number
        // not fully tested
        // ended up with GetSingleItem() instead
        public static FindItemsAdvancedResponse FindByKeyword(UserSettingsView settings)
        {
            dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();

            // Setting the required proterty value

            CustomFindAdvanced service = new CustomFindAdvanced();
            service.Url = "http://svcs.ebay.com/services/search/FindingService/v1";
            service.appID = settings.AppID;
            FindItemsAdvancedRequest request = new FindItemsAdvancedRequest();
            request.keywords = "302704549832";
            //var p = new ProductId();
            //p.type = "ReferenceID";
            ////p.type = "UPC";
            //p.Value = "222903428290";
            //p.Value = "302704549832";
            ////p.Value = "0019649215775";
            //request.productId = p;

            // Setting the pagination 
            PaginationInput pagination = new PaginationInput();
            pagination.entriesPerPageSpecified = true;
            pagination.entriesPerPage = 100;
            pagination.pageNumberSpecified = true;
            pagination.pageNumber = 1;
            request.paginationInput = pagination;

            FindItemsAdvancedResponse response = service.findItemsAdvanced(request);
            return response;
        }

        public static FindCompletedItemsResponse FindCompletedItems(string seller, int daysBack, string appID, int pageNumber)
        {
            try
            {
                CustomFindSold service = new CustomFindSold();
                service.Url = "http://svcs.ebay.com/services/search/FindingService/v1";
                service.appID = appID;
                FindCompletedItemsRequest request = new FindCompletedItemsRequest();

                ItemFilter filterSeller = new ItemFilter();
                filterSeller.name = ItemFilterType.Seller;
                filterSeller.paramName = "name";
                filterSeller.paramValue = "Seller";
                filterSeller.value = new string[] { seller };

                DateTime ModTimeTo = DateTime.Now.ToUniversalTime();
                DateTime ModTimeFrom = ModTimeTo.AddDays(-daysBack);
                string ModTimeToStr = ModTimeTo.Year + "-" + ModTimeTo.Month.ToString("00") + "-" + ModTimeTo.Day.ToString("00") + "T00:00:00.000Z";
                string ModTimeFromStr = ModTimeFrom.Year + "-" + ModTimeFrom.Month.ToString("00") + "-" + ModTimeFrom.Day.ToString("00") + "T00:00:00.000Z";

                ItemFilter filterEndTimeFrom = new ItemFilter();
                filterEndTimeFrom.name = ItemFilterType.EndTimeFrom;
                filterEndTimeFrom.value = new string[] { ModTimeFromStr };

                ItemFilter filterEndTimeTo = new ItemFilter();
                filterEndTimeTo.name = ItemFilterType.EndTimeTo;
                filterEndTimeTo.value = new string[] { ModTimeToStr };

                ItemFilter filterSoldOnly = new ItemFilter();
                filterSoldOnly.name = ItemFilterType.SoldItemsOnly;
                filterSoldOnly.value = new string[] { "true" };

                //Create the filter array
                ItemFilter[] itemFilters = new ItemFilter[3];

                //Add Filters to the array
                itemFilters[0] = filterSeller;
                itemFilters[1] = filterEndTimeFrom;
                itemFilters[2] = filterEndTimeTo;

                request.itemFilter = itemFilters;

                // Setting the pagination 
                PaginationInput pagination = new PaginationInput();
                pagination.entriesPerPageSpecified = true;
                pagination.entriesPerPage = 100;
                pagination.pageNumberSpecified = true;
                pagination.pageNumber = pageNumber;
                request.paginationInput = pagination;

                // Sorting the result
                request.sortOrderSpecified = true;
                request.sortOrder = SortOrderType.EndTimeSoonest;

                FindCompletedItemsResponse response = service.findCompletedItems(request);

                int totalPages = response.paginationOutput.totalPages;

                if (response.searchResult.item != null)
                    return response;
                else return null;
            }
            catch (Exception ex)
            {
                throw (ex);
            }
        }

        public static int FindItemsMain(UserSettingsView settings, string seller, int daysBack)
        {
            int total = 0;
            int currentPageNumber = 1;
            var response = FindItems(settings, seller, currentPageNumber, daysBack);
            total = response.searchResult.count;
            dsutil.DSUtil.WriteFile(_logfile, "Retrieve sales complete", settings.UserName);

            if (response.ack == AckValue.Success)
            {
                var result = response.searchResult;
                if (result != null && result.count > 0)
                {
                    // are there more pages of results?
                    for (var i = response.paginationOutput.pageNumber; i < response.paginationOutput.totalPages; i++)
                    {
                        currentPageNumber += 1;
                        response = FindItems(settings, seller, currentPageNumber, daysBack);
                        if (response.ack == AckValue.Success)
                        {
                            total += response.searchResult.count;
                            result = response.searchResult;
                        }
                    }
                }
            }
            return total;
        }

        // uses operation 'findItemsAdvanced'
        protected static FindItemsAdvancedResponse FindItems(UserSettingsView settings, string seller, int pageNumber, int daysBack)
        {
            StringBuilder strResult = new StringBuilder();
            try
            {
                DateTime ModTimeTo = DateTime.Now.ToUniversalTime();
                DateTime ModTimeFrom = ModTimeTo.AddDays(-daysBack);
                string ModTimeToStr = ModTimeTo.Year + "-" + ModTimeTo.Month.ToString("00") + "-" + ModTimeTo.Day.ToString("00") + "T00:00:00.000Z";
                string ModTimeFromStr = ModTimeFrom.Year + "-" + ModTimeFrom.Month.ToString("00") + "-" + ModTimeFrom.Day.ToString("00") + "T00:00:00.000Z";

                CustomFindAdvanced service = new CustomFindAdvanced();
                service.Url = "http://svcs.ebay.com/services/search/FindingService/v1";
                service.appID = settings.AppID;
                FindItemsAdvancedRequest request = new FindItemsAdvancedRequest();

                // Setting the required proterty value
                //request.keywords = keyword.Trim();

                //Create Filter Objects
                ItemFilter filterEndTimeFrom = new ItemFilter();
                ItemFilter filterEndTimeTo = new ItemFilter();
                ItemFilter filterCatID = new ItemFilter();

                //Set Values for each filter
                filterEndTimeFrom.name = ItemFilterType.EndTimeFrom;
                filterEndTimeFrom.value = new string[] { ModTimeFromStr };

                filterEndTimeTo.name = ItemFilterType.EndTimeTo;
                filterEndTimeTo.value = new string[] { ModTimeToStr };

                filterCatID.name = ItemFilterType.EndTimeFrom;
                filterCatID.value = new string[] { "" };

                ItemFilter filterSeller = new ItemFilter();
                filterSeller.name = ItemFilterType.Seller;
                filterSeller.paramName = "name";
                filterSeller.paramValue = "Seller";
                filterSeller.value = new string[] { seller };

                //Create the filter array
                ItemFilter[] itemFilters = new ItemFilter[1];

                //Add Filters to the array
                itemFilters[0] = filterSeller;

                request.itemFilter = itemFilters;

                // Setting the pagination 
                PaginationInput pagination = new PaginationInput();
                pagination.entriesPerPageSpecified = true;
                pagination.entriesPerPage = 50;
                pagination.pageNumberSpecified = true;
                pagination.pageNumber = pageNumber;

                request.paginationInput = pagination;

                // Sorting the result
                request.sortOrderSpecified = true;
                request.sortOrder = SortOrderType.StartTimeNewest;

                FindItemsAdvancedResponse response = service.findItemsAdvanced(request);
                if (response.searchResult.count > 0)
                {
                    foreach (SearchItem searchItem in response.searchResult.item)
                    {
                        strResult.AppendLine("ItemID: " + searchItem.itemId);
                        strResult.AppendLine("Title: " + searchItem.title);
                        strResult.AppendLine("Variation: " + searchItem.isMultiVariationListing.ToString());
                        strResult.AppendLine("Type: " + searchItem.listingInfo.listingType);
                        strResult.AppendLine("View: " + searchItem.viewItemURL);
                        strResult.AppendLine("Price: " + searchItem.sellingStatus.currentPrice.Value);
                        strResult.AppendLine("Picture: " + searchItem.galleryURL);
                        strResult.AppendLine("SellingStatus: " + searchItem.sellingStatus.sellingState);
                        strResult.AppendLine("------------------------------------------------------------------------");

                        using (System.IO.StreamWriter file =
                            new System.IO.StreamWriter(@"C:\temp\WriteLines2.txt", true))
                        {
                            file.WriteLine(searchItem.title + "\t" + searchItem.isMultiVariationListing.ToString());
                        }
                    }
                }
                else
                {
                    strResult.AppendLine("No result found...Try with other keyword(s)");
                }
                Console.WriteLine("");
                Console.WriteLine(strResult.ToString());
                Console.WriteLine("Total Pages: " + response.paginationOutput.totalPages);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return null;
            }
        }

        // uses operation 'findItemsByKeywords'
        protected static void SOAPSearch(string keyword)
        {
            StringBuilder strResult = new StringBuilder();
            try
            {
                CustomFindingService service = new CustomFindingService();
                service.Url = "http://svcs.ebay.com/services/search/FindingService/v1";
                FindItemsByKeywordsRequest request = new FindItemsByKeywordsRequest();

                // Setting the required proterty value
                request.keywords = keyword.Trim();

                // Setting the pagination 
                PaginationInput pagination = new PaginationInput();
                pagination.entriesPerPageSpecified = true;
                pagination.entriesPerPage = 25;
                pagination.pageNumberSpecified = true;
                pagination.pageNumber = 1;
                request.paginationInput = pagination;

                // Sorting the result
                request.sortOrderSpecified = true;
                request.sortOrder = SortOrderType.CurrentPriceHighest;

                FindItemsByKeywordsResponse response = service.findItemsByKeywords(request);
                if (response.searchResult.count > 0)
                {
                    foreach (SearchItem searchItem in response.searchResult.item)
                    {
                        strResult.AppendLine("ItemID: " + searchItem.itemId);
                        strResult.AppendLine("Title: " + searchItem.title);
                        strResult.AppendLine("Type: " + searchItem.listingInfo.listingType);
                        strResult.AppendLine("Price: " + searchItem.sellingStatus.currentPrice.Value);
                        strResult.AppendLine("Picture: " + searchItem.galleryURL);
                        strResult.AppendLine("------------------------------------------------------------------------");
                    }
                }
                else
                {
                    strResult.AppendLine("No result found...Try with other keyword(s)");
                }
                Console.WriteLine("");
                Console.WriteLine(strResult.ToString());
                Console.WriteLine("Total Pages: " + response.paginationOutput.totalPages);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        /// <summary>
        /// BETA
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="itemID"></param>
        /// <param name="ModTimeFrom"></param>
        /// <param name="ModTimeTo"></param>
        public static void ProcessTransactions(UserSettingsView settings, string itemID, DateTime ModTimeFrom, DateTime ModTimeTo)
        {
            var transactions = eBayUtility.ebayAPIs.GetItemTransactions(settings, itemID, ModTimeFrom, ModTimeTo);
            foreach (TransactionType item in transactions)
            {
                // did it sell?
                if (item.MonetaryDetails != null)
                {
                    var pmtTime = item.MonetaryDetails.Payments.Payment[0].PaymentTime;
                    var pmtAmt = item.MonetaryDetails.Payments.Payment[0].PaymentAmount.Value;
                    var order = new OrderHistoryDetail();
                    // order.Title = searchItem.title;
                    order.Qty = item.QuantityPurchased;

                    if (item.TransactionPrice == null)
                    {
                        // is this bcs sellerPaidStatus="notpaid"?
                        order.Price = 0;
                    }
                    else
                    {
                        var sellerPrice = item.TransactionPrice.Value.ToString();
                    }

                    order.DateOfPurchase = item.CreatedDate;

                    //order.EbayUrl = searchItem.viewItemURL;
                    // dsutil.DSUtil.WriteFile(_logfile, "order.EbayUrl complete", user.UserName);

                    //order.ImageUrl = searchItem.galleryURL;
                    dsutil.DSUtil.WriteFile(_logfile, "order.ImageUrl complete", settings.UserName);

                    //var pictures = searchItem.pictureURLLarge;
                    // dsutil.DSUtil.WriteFile(_logfile, "pictures complete", user.UserName);

                    //order.PageNumber = pg;
                    //order.ItemId = searchItem.itemId;
                    //order.SellingState = searchItem.sellingStatus.sellingState;
                    //order.ListingStatus = i.ListingStatus;
                    //order.IsMultiVariationListing = isVariation;

                    // order.ShippingServiceCost = i.ShippingServiceCost;
                    // order.ShippingServiceName = i.ShippingServiceName;

                    //orderHistory.Add(order);
                }
            }
        }

        public static string FormateBayTime(DateTime dt)
        {
            string dtStr = dt.Year + "-" + dt.Month.ToString("00") + "-" + dt.Day.ToString("00") + "T" + dt.Hour.ToString("00") + ":" + dt.Minute.ToString("00") + ":00.000Z";
            return dtStr;
        }

        /// <summary>
        /// Build request to fetch seller's sales
        /// 07.05.2019 needs further clarification - eBay told me a completed listing is a listing that ended which may or may not have sold,
        /// but that's not what this returns.
        /// </summary>
        /// <param name="seller"></param>
        /// <param name="daysBack"></param>
        /// <returns></returns>
        public static FindCompletedItemsRequest BuildReqest(string seller, DateTime fromDate)
        {
            FindCompletedItemsRequest request = new FindCompletedItemsRequest();

            ItemFilter filterSeller = new ItemFilter();
            filterSeller.name = ItemFilterType.Seller;
            filterSeller.value = new string[] { seller };

            DateTime ModTimeTo = DateTime.Now.ToUniversalTime();
            string ModTimeToStr = FormateBayTime(ModTimeTo);
            string ModTimeFromStr = FormateBayTime(fromDate);

            ItemFilter filterEndTimeFrom = new ItemFilter();
            filterEndTimeFrom.name = ItemFilterType.EndTimeFrom;
            filterEndTimeFrom.value = new string[] { ModTimeFromStr };

            ItemFilter filterEndTimeTo = new ItemFilter();
            filterEndTimeTo.name = ItemFilterType.EndTimeTo;
            filterEndTimeTo.value = new string[] { ModTimeToStr };

            ItemFilter filterSoldOnly = new ItemFilter();
            filterSoldOnly.name = ItemFilterType.SoldItemsOnly;
            filterSoldOnly.value = new string[] { "true" };

            //Create the filter array
            ItemFilter[] itemFilters = new ItemFilter[3];

            //Add Filters to the array
            itemFilters[0] = filterSeller;
            itemFilters[1] = filterEndTimeFrom;
            itemFilters[2] = filterEndTimeTo;

            request.itemFilter = itemFilters;
            return request;
        }

        public static eBay.Service.Core.Soap.ShippingDetailsType GetShippingDetail(ShippingService shippingService)
        {
            eBay.Service.Core.Soap.ShippingDetailsType sd = new eBay.Service.Core.Soap.ShippingDetailsType();

            //sd.ApplyShippingDiscount = true;
            //sd.PaymentInstructions = "eBay .Net SDK test instruction.";
            //sd.ShippingRateType = ShippingRateTypeCodeType.StandardList;

            //adding domestic shipping

            ShippingServiceOptionsType domesticShipping1 = new ShippingServiceOptionsType();

            if (shippingService == ShippingService.Standard)
            {
                domesticShipping1.ShippingService = ShippingServiceCodeType.ShippingMethodStandard.ToString();    // displays as "Standard Shipping" but for my account FAST 'N FREE
            }
            if (shippingService == ShippingService.Economy)
            {
                domesticShipping1.ShippingService = ShippingServiceCodeType.Other.ToString();                       // displays as "Economy Shipping" (slower shipping time)
            }
            domesticShipping1.ShippingServiceCost = new eBay.Service.Core.Soap.AmountType { Value = 0, currencyID = eBay.Service.Core.Soap.CurrencyCodeType.USD };
            domesticShipping1.ShippingInsuranceCost = new eBay.Service.Core.Soap.AmountType { Value = 0, currencyID = eBay.Service.Core.Soap.CurrencyCodeType.USD };
            domesticShipping1.ShippingServicePriority = 4;
            domesticShipping1.LocalPickup = false;
            domesticShipping1.FreeShipping = true;

            var s = new DispatchTimeMaxDetailsType();
            s.DispatchTimeMax = 3;

            
            sd.ShippingServiceOptions = new ShippingServiceOptionsTypeCollection(new[] { domesticShipping1 });
            sd.ShippingType = eBay.Service.Core.Soap.ShippingTypeCodeType.Flat;

            return sd;
        }

        /// <summary>
        /// Retrieve item details.
        /// </summary>
        /// <param name="ItemID">eBay Item ID</param>
        public static void GetItemRequest(UserSettingsView settings, string ItemID, string siteID)
        {
            eBayAPIInterfaceService service = EbayCalls.eBayServiceCall(settings, "GetItem", siteID);

            GetItemRequestType request = new GetItemRequestType();
            request.Version = "949";
            request.ItemID = ItemID;
            GetItemResponseType response = service.GetItem(request);

            Console.WriteLine("=====================================");
            Console.WriteLine("Item Iitle - {0}", response.Item.Title);
            Console.WriteLine("=====================================");

            Console.WriteLine("ItemID: {0}", response.Item.ItemID);
            Console.WriteLine("Primary Category: {0}", response.Item.PrimaryCategory.CategoryName);
            Console.WriteLine("Listing Duration: {0}", response.Item.ListingDuration);
            Console.WriteLine("Start Price: {0} {1}", response.Item.StartPrice.Value, response.Item.Currency);
            Console.WriteLine("Payment Type[0]: {0}", response.Item.PaymentMethods[0]);
            Console.WriteLine("PayPal Email Address: {0}", response.Item.PayPalEmailAddress);
            Console.WriteLine("Postal Code: {0}", response.Item.PostalCode);
            // ...Convert response object to JSON to see all
        }

    }
}