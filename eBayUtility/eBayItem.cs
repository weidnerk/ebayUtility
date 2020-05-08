/*
 * Listing functions.
 * 
 * This file is heavily dependent on the .NET SDK (References/eBay.Service)
 * (download here: https://developer.ebay.com/tools/netsdk)
 * but different in that calls go through the eBayAPIInterfaceService object.
 * 
 * https://www.yumpu.com/en/document/read/6321452/trading-api-ebay-developers
 * 
 */
using dsmodels;
using dsutil;
using eBay.Service.Call;
using eBay.Service.Core.Sdk;
using eBay.Service.Core.Soap;
using eBay.Service.Util;
using eBayUtility;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Services.Protocols;
using System.Xml.Linq;

namespace Utility
{
    public enum ShippingCostPaidBy
    {
        Buyer,
        Seller
    }
    public enum ShippingService
    {
        Standard,
        Economy
    }
    public class eBayItem
    {
        static dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
        const int _qtyToList = 2;
        const string _logfile = "log.txt";

        /// <summary>
        /// Get both eBay user id and paypal address
        /// </summary>
        /// <param name="storeID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        public static eBayUser GeteBayUser(int storeID, string userID)
        {
            string token = db.GetToken(storeID, userID);
            var eBayUser = GeteBayUser(token);
            return eBayUser;
        }
        public static eBayUser GeteBayUser(string token)
        {
            var eBayUser = GetUser(token);
            var pref = GetUserPreferences(token);
            eBayUser.PayPalEmail = pref.PayPalEmail;
            return eBayUser;
        }

        public static eBayUser GetUser(int storeID, string userID)
        {
            string token = db.GetToken(storeID, userID);
            return GetUser(token);
        }

        public static eBayUser GetUser(string token)
        {
            /*
             * https://developer.ebay.com/devzone/xml/docs/reference/ebay/GetUser.html
             * 
             */
            try
            {
                ApiContext context = new ApiContext();

                //set the User token
                
                context.ApiCredential.eBayToken = token;

                //set the version
                context.Version = "817";
                context.Site = eBay.Service.Core.Soap.SiteCodeType.US;
                var request = new GetUserCall(context);

                request.Execute();
                // eagle came back as 'Basic'
                string result = request.User.Email;
                bool newUser = request.User.NewUser;
                string name = request.User.UserID;
                var user = new eBayUser
                {
                    eBayUserID = name
                };
                return user;
                // string result = request.ApiResponse.Ack + " Ended ItemID " + request.;
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetUser", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "");
                throw;
            }
        }
        public static eBayUser GetUserPreferences(int storeID, string userID)
        {
            string token = db.GetToken(storeID, userID);
            return GetUserPreferences(token);
        }
        /// <summary>
        /// https://developer.ebay.com/devzone/xml/docs/reference/ebay/GetUserPreferences.html
        /// </summary>
        /// <param name="userID"></param>
        public static eBayUser GetUserPreferences(string token)
        {
            try
            {
                ApiContext context = new ApiContext();

                //set the User token

                context.ApiCredential.eBayToken = token;

                //set the version
                context.Version = "817";
                context.Site = eBay.Service.Core.Soap.SiteCodeType.US;
                var request = new GetUserPreferencesCall(context);

                request.ShowSellerPaymentPreferences = true;
                request.Execute();

                // per the documenation, "Specifies the default email address the seller uses for receiving PayPal payments."
                string result = request.SellerPaymentPreferences.DefaultPayPalEmailAddress;
                // eagle came back as 'Basic'
                //string result = request.ApiResponse.Ack;
                var user = new eBayUser
                {
                    PayPalEmail = result
                };
                return user;
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetUserPreferences", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "");
                throw;
            }
        }
        public static eBayStore GetStore(int storeID, string userID)
        {
            string token = db.GetToken(storeID, userID);
            return GetStore(token);     // if get null here, means no subscription
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="storeID"></param>
        /// <param name="userID"></param>
        /// <returns>subscription name, or null if no subscription</returns>
        public static eBayStore GetStore(string token)
        {
            /*
             * https://developer.ebay.com/devzone/xml/docs/reference/ebay/GetStore.html
             * https://developer.ebay.com/devzone/xml/docs/reference/ebay/types/StoreSubscriptionLevelCodeType.html
             * For some reason, home-decor retuns CustomCode subscription but supposed to be Starter.
             * Returns Basic for eagle which is correct.
             * 
             */
            string storeName = null;
            string marker = "User must have a store subscription";
            try
            {
                ApiContext context = new ApiContext();

                //set the User token
                context.ApiCredential.eBayToken = token;

                //set the version
                context.Version = "817";
                context.Site = eBay.Service.Core.Soap.SiteCodeType.US;
                var request = new GetStoreCall(context);

                request.Execute();
                // eagle came back as 'Basic'
                string result = request.Store.SubscriptionLevel.ToString();
                storeName = request.Store.Name;
                var store = new eBayStore
                {
                    StoreName = storeName,
                    Subscription = result
                };
                return store;
                // string result = request.ApiResponse.Ack + " Ended ItemID " + request.;
            }
            catch (Exception exc)
            {
                int pos = exc.Message.ToUpper().IndexOf(marker.ToUpper());
                if (pos > -1)
                {
                    var store = new eBayStore
                    {
                        StoreName = storeName,
                        Subscription = "No subscription"
                    };
                    return store;
                }
                string msg = dsutil.DSUtil.ErrMsg("GetStore", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "");
                throw;
            }
        }

        /// <summary>
        /// https://developer.ebay.com/devzone/finding/concepts/MakingACall.html
        /// https://developer.ebay.com/Devzone/business-policies/Concepts/MakingACall.html#TestingOverview
        /// </summary>
        /// <param name="settings"></param>
        public static eBayBusinessPolicies GetSellerBusinessPolicy(UserSettingsView settings)
        {
            // gotta look at this, GetShippingCosts()

            var policies = new eBayBusinessPolicies();
            var shippingPolicies = new List<ShippingPolicy>();
            var returnPolicies = new List<ReturnPolicy>();
            var paymentPolicies = new List<PaymentPolicy>();
            try
            {
                var uri = new Uri("https://svcs.ebay.com/services/selling/v1/SellerProfilesManagementService");
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);

                request.Headers.Add("X-EBAY-SOA-SECURITY-TOKEN", settings.Token);
                request.Headers.Add("X-EBAY-SOA-OPERATION-NAME", "getSellerProfiles");
                request.Headers.Add("X-EBAY-SOA-SERVICE-NAME", "SellerProfilesManagementService");
                request.Headers.Add("X-EBAY-SOA-SERVICE-VERSION", "1.0.0");
                request.Headers.Add("X-EBAY-SOA-GLOBAL-ID", "EBAY-US");

                using (WebResponse Serviceres = request.GetResponse())
                {
                    using (StreamReader rd = new StreamReader(Serviceres.GetResponseStream()))
                    {
                        // reading stream    
                        // https://stackoverflow.com/questions/692342/net-httpwebrequest-getresponse-raises-exception-when-http-status-code-400-ba

                        var ServiceResult = rd.ReadToEnd();
                        //writting stream result on console    
                        var s = ServiceResult.Replace("\"", "'");
                        var output = s.Replace(" xmlns='http://www.ebay.com/marketplace/selling/v1/services'", string.Empty);
                        XElement root = XElement.Parse(output);

                        // SHIPPING POLICIES
                        var qryRecords = from record in root.Elements("shippingPolicyProfile").Elements("ShippingPolicyProfile").Elements("shippingPolicyInfo")
                                         select record;
                        if (qryRecords.Count() == 0)
                        {
                            shippingPolicies = null;
                        }
                        else
                        {
                            int y = qryRecords.Count();
                            foreach (var item in qryRecords)
                            {
                                var shippingPolicy = new ShippingPolicy();
                                shippingPolicy.Name = item.Element("shippingPolicyName").Value;
                                shippingPolicy.HandlingTime = Convert.ToInt32(item.Element("dispatchTimeMax").Value);
                                shippingPolicy.ShippingService = item.Element("domesticShippingPolicyInfoService").Element("shippingService").Value;
                                shippingPolicy.GlobalShipping = Convert.ToBoolean(item.Element("GlobalShipping").Value);
                                shippingPolicies.Add(shippingPolicy);
                            }
                        }

                        // RETURN POLICIES
                        qryRecords = from record in root.Elements("returnPolicyProfileList").Elements("ReturnPolicyProfile")
                                         select record;
                        if (qryRecords.Count() == 0)
                        {
                            returnPolicies = null;
                        }
                        else
                        {
                            int y = qryRecords.Count();
                            foreach (var item in qryRecords)
                            {
                                var returnPolicy = new ReturnPolicy();
                                returnPolicy.Name = item.Element("profileName").Value;
                                returnPolicy.ShippingCostPaidByOption = item.Element("returnPolicyInfo").Element("shippingCostPaidByOption").ElementValueNull(); ;
                                returnPolicies.Add(returnPolicy);
                            }
                        }
                        // PAYMENT POLICIES
                        qryRecords = from record in root.Elements("paymentProfileList").Elements("PaymentProfile")
                                     select record;
                        if (qryRecords.Count() == 0)
                        {
                            paymentPolicies = null;
                        }
                        else
                        {
                            int y = qryRecords.Count();
                            foreach (var item in qryRecords)
                            {
                                var paymentPolicy = new PaymentPolicy();
                                paymentPolicy.Name = item.Element("profileName").Value;
                                paymentPolicy.PaypalEmailAddress = item.Element("paymentInfo").Element("paypalEmailAddress").Value;
                                paymentPolicies.Add(paymentPolicy);
                            }
                        }
                    }
                }
                policies.ShippingPolicies = shippingPolicies;
                policies.ReturnPolicies = returnPolicies;
                policies.PaymentPolicies = paymentPolicies;
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetSellerBusinessPolicy", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                throw;
            }
            return policies;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemID">ebay seller listing id</param>
        /// <returns></returns>
        public static async Task<List<string>> ListingCreateAsync(
            UserSettingsView settings, 
            int listingID)
        {
            var output = new List<string>();
            var listing = db.ListingGet(listingID);     // item has to be stored before it can be listed
            var token = db.GetToken(settings);

            if (listing != null)
            {
                // if item is listed already, then revise
                if (listing.Listed == null)
                {
                    if (string.IsNullOrEmpty(listing.PictureURL))
                    {
                        output.Add("ERROR: PictureURL is null");
                        return output;
                    }
                    List<string> pictureURLs = dsutil.DSUtil.DelimitedToList(listing.PictureURL, ';');

                    string verifyItemID = null;

                    string shippingProfile = settings.ShippingProfile;
                    string paymentProfile = settings.PaymentProfile;
                    string returnProfile = settings.ReturnProfile;

                    //shippingProfile = "Flat:Economy Shippi(Free),4 business days";
                    //paymentProfile = "PayPal:Immediate pay#0";
                    //returnProfile = "Returns Accepted,Buyer,30 Days,Money Back";

                    // Is the user setup with Business Policies?  Probably not best way to do it.
                    //if (!string.IsNullOrEmpty(settings.ShippingProfile))
                    //{
                        verifyItemID = eBayItem.VerifyAddItemRequest(settings, listing.ListingTitle,
                            listing.Description,
                            listing.PrimaryCategoryID,
                            (double)listing.ListingPrice,
                            pictureURLs,
                            ref output,
                            listing.Qty,
                            listing,
                            shippingProfile,
                            returnProfile,
                            paymentProfile);
                    //}
                    //else
                    //{
                    //    verifyItemID = eBayItem.VerifyAddItemRequest(settings, listing.ListingTitle,
                    //        listing.Description,
                    //        listing.PrimaryCategoryID,
                    //        (double)listing.ListingPrice,
                    //        pictureURLs,
                    //        ref output,
                    //        listing.Qty,
                    //        listing,
                    //        4,
                    //        ShippingCostPaidBy.Buyer,
                    //        ShippingService.Economy,
                    //        "ventures2019@gmail.com");
                    //}
                    // at this point, 'output' will be populated with errors if any occurred

                    if (!string.IsNullOrEmpty(verifyItemID))
                    {
                        // make sure listedItemID is first in list
                        output.Insert(0, "Listed: YES");
                        output.Insert(0, verifyItemID);
                        if (!listing.Listed.HasValue)
                        {
                            listing.Listed = DateTime.Now;
                        }
                        var response = FlattenList(output);
                        await db.ListedItemIDUpdate(listing, verifyItemID, settings.UserID, true, response);
                    }
                    else
                    {
                        output.Add("Listing not created.");
                    }
                    if (output.Count > 0)
                    {
                        await LogListingResponse(settings, listing, output);
                    }
                }
                else
                {
                    string response = null;
                    output = ReviseItem(token,
                                        listing.ListedItemID,
                                        qty: listing.Qty,
                                        price: Convert.ToDouble(listing.ListingPrice),
                                        title: listing.ListingTitle,
                                        description: listing.Description);
                    var log = new ListingLog();
                    log.UserID = settings.UserID;
                    log.MsgID = 800;
                    log.Note = "revised listing by " + settings.UserName;
                    log.ListingID = listing.ID;
                    await db.ListingLogAdd(log);
                    if (output.Count > 0)
                    {
                        response = FlattenList(output);
                    }
                    // update the 'updatedby' fields
                    await db.ListedItemIDUpdate(listing, listing.ListedItemID, settings.UserID, true, response, updated: DateTime.Now);
                    output.Insert(0, listing.ListedItemID);

                    if (output.Count > 0)
                    {
                        await LogListingResponse(settings, listing, output);
                    }
                }
            }
            return output;
        }
        protected static async Task LogListingResponse(UserSettingsView settings, Listing listing, List<string> response)
        {
            try
            {
                var output = dsutil.DSUtil.ListToDelimited(response.ToArray(), ';');
                var log = new ListingLog();
                log.MsgID = 800;
                log.Note = output;
                log.UserID = settings.UserID;
                log.ListingID = listing.ID;
                await db.ListingLogAdd(log);
            }
            catch
            {
                throw;
            }
        }
        protected static string FlattenList(List<string> errors)
        {
            string output = null;
            foreach (string s in errors)
            {
                output += s + ";";
            }
            var r = output.Substring(0, output.Length - 1);
            return r;
        }


        /// <summary>
        /// Verify whether item is ready to be added to eBay.
        /// 
        /// Return listedItemID, output error 
        /// My presets are: 
        ///     NEW condition 
        ///     BuyItNow fixed price
        ///     30 day duration
        ///     14-day returns w/ 20% restocking fee
        ///     payment method=PayPal
        ///     FREE shipping
        ///     buyer pays for return shipping
        /// </summary>
        public static string VerifyAddItemRequest(UserSettingsView settings,
            string title,
            string description,
            string categoryID,
            double price,
            List<string> pictureURLs,
            ref List<string> errors,
            int qtyToList,
            Listing listing,
            string shippingProfile,
            string returnProfile,
            string paymentProfile)
        {
            //errors = null;
            string listedItemID = null;
            try
            {
                eBayAPIInterfaceService service = EbayCalls.eBayServiceCall(settings, "VerifyAddItem");
                
                VerifyAddItemRequestType request = new VerifyAddItemRequestType();
                request.Version = "949";
                request.ErrorLanguage = "en_US";
                request.WarningLevel = WarningLevelCodeType.High;

                var item = new ItemType();

                item.Title = title;
                item.Description = description;
                item.PrimaryCategory = new CategoryType
                {
                    CategoryID = categoryID
                };
                item.StartPrice = new AmountType
                {
                    Value = price,
                    currencyID = CurrencyCodeType.USD
                };

                // To view ConditionIDs follow the URL
                // http://developer.ebay.com/devzone/guides/ebayfeatures/Development/Desc-ItemCondition.html#HelpingSellersChoosetheRightCondition
                item.ConditionID = 1000;    // new
                item.Country = CountryCodeType.US;
                item.Currency = CurrencyCodeType.USD;
                // item.DispatchTimeMax = 2;       // pretty sure this is handling time

                // https://developer.ebay.com/devzone/xml/docs/reference/ebay/types/ListingDurationCodeType.html
                item.ListingDuration = "Days_30";
                item.ListingDuration = "GTC";

                // Buy It Now fixed price
                item.ListingType = ListingTypeCodeType.FixedPriceItem;
                // Auction
                //item.ListingType = ListingTypeCodeType.Chinese; 

                /*
                item.PaymentMethods = new BuyerPaymentMethodCodeTypeCollection
                {
                    BuyerPaymentMethodCodeType.PayPal
                };
                item.AutoPay = true;    // require immediate payment
                                        // Default testing paypal email address
                item.PayPalEmailAddress = "ventures2019@gmail.com";
                */

                item.PictureDetails = new PictureDetailsType();
                item.PictureDetails.PictureURL = new StringCollection();
                item.PictureDetails.PictureURL.AddRange(pictureURLs.ToArray());
                // item.PostalCode = "33772";
                item.Location = "Multiple locations";
                item.Quantity = qtyToList;

                item.ItemSpecifics = new NameValueListTypeCollection();

                NameValueListTypeCollection ItemSpecs = new NameValueListTypeCollection();

                var nv1 = new eBay.Service.Core.Soap.NameValueListType();
                var nv2 = new eBay.Service.Core.Soap.NameValueListType();
                StringCollection valueCol1 = new StringCollection();
                StringCollection valueCol2 = new StringCollection();

                if (!ItemSpecificExists(listing.ItemSpecifics, "Brand"))
                {
                    nv1.Name = "Brand";
                    valueCol1.Add("Unbranded");
                    nv1.Value = valueCol1;
                    ItemSpecs.Add(nv1);
                }
                if (!ItemSpecificExists(listing.ItemSpecifics, "MPN"))
                {
                    nv2.Name = "MPN";
                    valueCol2.Add("Does Not Apply");
                    nv2.Value = valueCol2;
                    ItemSpecs.Add(nv2);
                }

                var revisedItemSpecs = ModifyItemSpecific(listing.ItemSpecifics);
                foreach (var i in revisedItemSpecs)
                {
                    var n = AddItemSpecifics(i);
                    ItemSpecs.Add(n);
                }
                item.ItemSpecifics = ItemSpecs;

                var pd = new ProductListingDetailsType();
                //var brand = new BrandMPNType();
                //brand.Brand = "Unbranded";
                //brand.MPN = unavailable;
                //pd.BrandMPN = brand;
                pd.UPC = "Does not apply";
                item.ProductListingDetails = pd;

                string returnDescr = "Please return if unstatisfied.";
                // returnDescr = "30 day returns. Buyer pays for return shipping";
                var sp = new SellerProfilesType();

                var spp = new SellerPaymentProfileType();
                spp.PaymentProfileName = paymentProfile;

                var srp = new SellerReturnProfileType();
                srp.ReturnProfileName = returnProfile;

                var ssp = new SellerShippingProfileType();
                ssp.ShippingProfileName = shippingProfile;

                sp.SellerPaymentProfile = spp;
                sp.SellerReturnProfile = srp;
                sp.SellerShippingProfile = ssp;
                item.SellerProfiles = sp;

                /*
                 * How to create policy in place:
                 * 
                item.ReturnPolicy = new ReturnPolicyType
                {
                    ReturnsAcceptedOption = "ReturnsAccepted",
                    ReturnsWithinOption = "Days_30",
                    //RefundOption = "MoneyBack",
                    //Description = returnDescr,
                    ShippingCostPaidByOption = "Seller"
                    //,
                    //RestockingFeeValue = "Percent_20",
                    //RestockingFeeValueOption = "Percent_20"
                };
                item.ShippingDetails = GetShippingDetail();
                */
                // item.DispatchTimeMax = 3;   // aka handling time

                item.Site = SiteCodeType.US;

                request.Item = item;

                VerifyAddItemResponseType response = service.VerifyAddItem(request);
                Console.WriteLine("ItemID: {0}", response.ItemID);

                // If item is verified, the item will be added.
                if (response.ItemID == "0")
                {
                    listedItemID = AddItemRequest(settings, item, ref errors);
                }
                else
                {
                    foreach (ErrorType e in response.Errors)
                    {
                        errors.Add(e.LongMessage);
                    }
                }
                return listedItemID;
            }
            catch (SoapException exc)
            {
                string s = exc.Message;
                errors.Add(exc.Detail.InnerText);
                return null;
            }
            catch (Exception exc)
            {
                string s = exc.Message;
                errors.Add(s);
                return null;
            }
        }
        public static string VerifyAddItemRequest_no_policies(UserSettingsView settings,
            string title,
            string description,
            string categoryID,
            double price,
            List<string> pictureURLs,
            ref List<string> errors,
            int qtyToList,
            Listing listing,
            int handlingTime,
            ShippingCostPaidBy shippingCostPaidByOption,
            ShippingService shippingService,
            string payPalEmail)
        {
            //errors = null;
            string listedItemID = null;
            try
            {
                eBayAPIInterfaceService service = EbayCalls.eBayServiceCall(settings, "VerifyAddItem");

                VerifyAddItemRequestType request = new VerifyAddItemRequestType();
                request.Version = "949";
                request.ErrorLanguage = "en_US";
                request.WarningLevel = WarningLevelCodeType.High;

                var item = new ItemType();

                item.Title = title;
                item.Description = description;
                item.PrimaryCategory = new CategoryType
                {
                    CategoryID = categoryID
                };
                item.StartPrice = new AmountType
                {
                    Value = price,
                    currencyID = CurrencyCodeType.USD
                };

                // To view ConditionIDs follow the URL
                // http://developer.ebay.com/devzone/guides/ebayfeatures/Development/Desc-ItemCondition.html#HelpingSellersChoosetheRightCondition
                item.ConditionID = 1000;    // new
                item.Country = CountryCodeType.US;
                item.Currency = CurrencyCodeType.USD;
                item.DispatchTimeMax = handlingTime;       // handling time

                // https://developer.ebay.com/devzone/xml/docs/reference/ebay/types/ListingDurationCodeType.html
                item.ListingDuration = "Days_30";
                item.ListingDuration = "GTC";

                // Buy It Now fixed price
                item.ListingType = ListingTypeCodeType.FixedPriceItem;
                // Auction
                //item.ListingType = ListingTypeCodeType.Chinese; 

                item.PaymentMethods = new BuyerPaymentMethodCodeTypeCollection
                {
                    BuyerPaymentMethodCodeType.PayPal
                };
                item.AutoPay = true;    // require immediate payment
                                        // Default testing paypal email address
                item.PayPalEmailAddress = payPalEmail;

                item.PictureDetails = new PictureDetailsType();
                item.PictureDetails.PictureURL = new StringCollection();
                item.PictureDetails.PictureURL.AddRange(pictureURLs.ToArray());
                // item.PostalCode = "33772";
                item.Location = "Multiple locations";
                item.Quantity = qtyToList;

                item.ItemSpecifics = new NameValueListTypeCollection();

                NameValueListTypeCollection ItemSpecs = new NameValueListTypeCollection();

                var nv1 = new eBay.Service.Core.Soap.NameValueListType();
                var nv2 = new eBay.Service.Core.Soap.NameValueListType();
                StringCollection valueCol1 = new StringCollection();
                StringCollection valueCol2 = new StringCollection();

                if (!ItemSpecificExists(listing.ItemSpecifics, "Brand"))
                {
                    nv1.Name = "Brand";
                    valueCol1.Add("Unbranded");
                    nv1.Value = valueCol1;
                    ItemSpecs.Add(nv1);
                }
                if (!ItemSpecificExists(listing.ItemSpecifics, "MPN"))
                {
                    nv2.Name = "MPN";
                    valueCol2.Add("Does Not Apply");
                    nv2.Value = valueCol2;
                    ItemSpecs.Add(nv2);
                }

                var revisedItemSpecs = ModifyItemSpecific(listing.ItemSpecifics);
                foreach (var i in revisedItemSpecs)
                {
                    var n = AddItemSpecifics(i);
                    ItemSpecs.Add(n);
                }
                item.ItemSpecifics = ItemSpecs;

                var pd = new ProductListingDetailsType();
                //var brand = new BrandMPNType();
                //brand.Brand = "Unbranded";
                //brand.MPN = unavailable;
                //pd.BrandMPN = brand;
                pd.UPC = "Does not apply";
                item.ProductListingDetails = pd;

                string returnDescr = "Please return if unstatisfied.";
                // returnDescr = "30 day returns. Buyer pays for return shipping";
                var sp = new SellerProfilesType();

                /*
                 * How to create policy in place:
                 */
                string shippingCostPaidByStr = ShippingCostPaidByToStr(shippingCostPaidByOption);
                item.ReturnPolicy = new ReturnPolicyType
                {
                    ReturnsAcceptedOption = "ReturnsAccepted",
                    ReturnsWithinOption = "Days_30",
                    //RefundOption = "MoneyBack",
                    //Description = returnDescr,
                    ShippingCostPaidByOption = shippingCostPaidByStr
                    //,
                    //RestockingFeeValue = "Percent_20",
                    //RestockingFeeValueOption = "Percent_20"
                };
                item.ShippingDetails = eBayUtility.ebayAPIs.GetShippingDetail(shippingService);
                
                item.Site = SiteCodeType.US;

                request.Item = item;

                VerifyAddItemResponseType response = service.VerifyAddItem(request);
                Console.WriteLine("ItemID: {0}", response.ItemID);

                // If item is verified, the item will be added.
                if (response.ItemID == "0")
                {
                    listedItemID = AddItemRequest(settings, item, ref errors);
                }
                else
                {
                    foreach (ErrorType e in response.Errors)
                    {
                        errors.Add(e.LongMessage);
                    }
                }
                return listedItemID;
            }
            catch (SoapException exc)
            {
                string s = exc.Message;
                errors.Add(exc.Message);    // both errors are informative
                errors.Add(exc.Detail.InnerText);
                return null;
            }
            catch (Exception exc)
            {
                string s = exc.Message;
                errors.Add(s);
                return null;
            }
        }
        protected static string ShippingCostPaidByToStr(ShippingCostPaidBy shippingCostPaidBy)
        {
            string shippingCostPaidByStr =  null;
            switch (shippingCostPaidBy)
            {
                case ShippingCostPaidBy.Buyer:
                    shippingCostPaidByStr = "Buyer";
                    break;

                case ShippingCostPaidBy.Seller:
                    shippingCostPaidByStr = "Seller";
                    break;
            }
            return shippingCostPaidByStr;
        }
        protected static eBay.Service.Core.Soap.NameValueListType AddItemSpecifics(ListingItemSpecific item)
        {
            var nv2 = new eBay.Service.Core.Soap.NameValueListType();
            StringCollection valueCol2 = new StringCollection();

            nv2.Name = item.ItemName;
            valueCol2.Add(item.ItemValue);
            nv2.Value = valueCol2;

            return nv2;
        }
        public static List<ListingItemSpecific> ModifyItemSpecific(List<ListingItemSpecific> itemSpecifics)
        {
            var specifics = new List<ListingItemSpecific>();
            foreach (var s in itemSpecifics)
            {
                if (!OmitSpecific(s.ItemName))
                {
                    specifics.Add(s);
                }
            }
            return specifics;
        }
        protected static bool ItemSpecificExists(List<ListingItemSpecific> itemSpecifics, string itemName)
        {
            foreach (var s in itemSpecifics)
            {
                if (s.ItemName == itemName)
                {
                    return true;
                }
            }
            return false;
        }
        protected static bool OmitSpecific(string name)
        {
            if (name == "Restocking Fee")
                return true;
            if (name == "All returns accepted")
                return true;
            if (name == "Item must be returned within")
                return true;
            if (name == "Refund will be given as")
                return true;
            if (name == "Return shipping will be paid by")
                return true;
            if (name == "Return policy details")
                return true;

            return false;
        }

        /// <summary>
        /// Add item to eBay. Once verified.
        /// </summary>
        /// <param name="item">Accepts ItemType object from VerifyAddItem method.</param>
        public static string AddItemRequest(UserSettingsView settings, ItemType item, ref List<string> errors)
        {
            eBayAPIInterfaceService service = EbayCalls.eBayServiceCall(settings, "AddItem");

            AddItemRequestType request = new AddItemRequestType();
            request.Version = "949";
            request.ErrorLanguage = "en_US";
            request.WarningLevel = WarningLevelCodeType.High;
            request.Item = item;

            AddItemResponseType response = service.AddItem(request);
            foreach (ErrorType e in response.Errors)
            {
                errors.Add(e.LongMessage);
            }
            Console.WriteLine("Item Added");
            Console.WriteLine("ItemID: {0}", response.ItemID); // Item ID
            return response.ItemID;
        }

        /// <summary>
        /// Had case in Jennifer account where ebay removed the listings in the store (2) I guess because it was trying
        /// to verify security.
        /// But ds109 doesn't know that so try to end listing and get error.  In this case, the error is:
        /// "For security reasons, please log in again"
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="listing"></param>
        /// <returns></returns>
        public static string EndFixedPriceItem(UserSettingsView settings, Listing listing, out bool auctionWasEnded)
        {
            const string actionEndedMarker = "The auction has already been closed.";

            try
            {
                auctionWasEnded = false;

                //create the context
                ApiContext context = new ApiContext();

                //set the User token
                string token = db.GetToken(settings);
                context.ApiCredential.eBayToken = token;

                //set the server url

                //enable logging
                context.ApiLogManager = new ApiLogManager();
                context.ApiLogManager.ApiLoggerList.Add(new FileLogger("logebay.txt", false, false, false));
                context.ApiLogManager.EnableLogging = true;

                //set the version
                context.Version = "817";
                context.Site = eBay.Service.Core.Soap.SiteCodeType.US;

                EndFixedPriceItemCall endFP = new EndFixedPriceItemCall(context);

                endFP.ItemID = listing.ListedItemID;
                endFP.EndingReason = EndReasonCodeType.NotAvailable;

                // if you try to end a listing that was already ended using the ebay website, then an exception is thrown:
                // "The auction has already been closed."
                endFP.Execute();
                string result = endFP.ApiResponse.Ack + " Ended ItemID " + endFP.ItemID;
                return result;
            }
            catch (Exception exc)
            {
                int pos = exc.Message.IndexOf(actionEndedMarker);
                if (pos > -1)
                {
                    auctionWasEnded = true;
                    string msg = exc.Message;
                    dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                    return msg;
                }
                else
                {
                    string msg = "ERROR EndFixedPriceItem (eBay removed listing?  Token problem?) listedItemID -> " + listing.ListedItemID + " -> " + exc.Message;
                    dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                    throw;
                }
            }
        }

        // use this for itemspecifics:
        // https://ebaydts.com/eBayKBDetails?KBid=1647


        //
        /// <summary>
        /// use this for itemspecifics:
        /// https://ebaydts.com/eBayKBDetails?KBid=1647
        /// 
        /// In order to set an item qty to 0, need to follow this:
        /// https://help.zentail.com/en/articles/2086059-error-the-quantity-must-be-a-valid-number-greater-than-0
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="listedItemID"></param>
        /// <param name="qty"></param>
        /// <param name="price"></param>
        /// <param name="title"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        public static List<string> ReviseItem(string token,
            string listedItemID,
            int? qty = null,
            double? price = null,
            string title = null,
            string description = null)
        {
            var response = new List<string>();
            try
            {
                ApiContext context = new ApiContext();

                //set the User token
                context.ApiCredential.eBayToken = token;

                //enable logging
                //context.ApiLogManager = new ApiLogManager();
                //context.ApiLogManager.ApiLoggerList.Add(new FileLogger("logebay.txt", false, false, false));
                /*
                 * PLEASE NOTE:
                 * Long time issue of finishing the listing revise but then navigating to another page is slow.
                 * Turn off logging fixes this.  Not sure what ebay logger is doing
                 */
                //context.ApiLogManager.EnableLogging = false;

                //set the version
                context.Version = "817";
                context.Site = eBay.Service.Core.Soap.SiteCodeType.US;

                ReviseFixedPriceItemCall reviseFP = new ReviseFixedPriceItemCall(context);

                ItemType item = new ItemType();
                item.ItemID = listedItemID;

                if (qty.HasValue)
                    item.Quantity = qty.Value;

                if (price.HasValue)
                {
                    item.StartPrice = new eBay.Service.Core.Soap.AmountType
                    {
                        Value = price.Value,
                        currencyID = eBay.Service.Core.Soap.CurrencyCodeType.USD
                    };
                }
                if (!string.IsNullOrEmpty(title))
                {
                    item.Title = title;
                }
                if (!string.IsNullOrEmpty(description))
                {
                    item.Description = description;
                }

                #region sample_code
                /*
                item.ItemSpecifics = new NameValueListTypeCollection();

                NameValueListTypeCollection ItemSpecs = new NameValueListTypeCollection();

                var nv1 = new eBay.Service.Core.Soap.NameValueListType();
                var nv2 = new eBay.Service.Core.Soap.NameValueListType();

                StringCollection valueCol1 = new StringCollection();
                StringCollection valueCol2 = new StringCollection();

                nv1.Name = "Brand";
                valueCol1.Add("Unbranded");
                nv1.Value = valueCol1;

                nv2.Name = "MPN";
                valueCol2.Add("Does Not Apply");
                nv2.Value = valueCol2;

                ItemSpecs.Add(nv1);
                ItemSpecs.Add(nv2);
                item.ItemSpecifics = ItemSpecs;

                var pd = new ProductListingDetailsType();
                //var brand = new BrandMPNType();
                //brand.Brand = "Unbranded";
                //brand.MPN = unavailable;
                //pd.BrandMPN = brand;
                pd.UPC = "Does not apply";
                item.ProductListingDetails = pd;
                */
                #endregion

                reviseFP.Item = item;

                reviseFP.Execute();
                var r = reviseFP.ApiResponse;
                string msg = r.Ack.ToString();
                if (r.Errors.Count > 0)
                {
                    foreach (eBay.Service.Core.Soap.ErrorType e in r.Errors)
                    {
                        // msg += " " + e.LongMessage;
                        response.Add(e.LongMessage);
                    }
                }
            }
            catch (Exception exc)
            {
                string msg = "ERROR ReviseItem listedItemID -> " + listedItemID + " -> " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, "");
                throw;
            }
            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="listing"></param>
        /// <returns></returns>
        public static List<string> ReviseItemSpecifics(UserSettingsView settings, Listing listing)
        {
            var response = new List<string>();
            //create the context
            ApiContext context = new ApiContext();

            //set the User token
            var token = db.GetToken(settings);
            context.ApiCredential.eBayToken = token;

            //enable logging
            context.ApiLogManager = new ApiLogManager();
            context.ApiLogManager.ApiLoggerList.Add(new FileLogger("logebay.txt", false, false, false));
            context.ApiLogManager.EnableLogging = true;

            //set the version
            context.Version = "817";
            context.Site = eBay.Service.Core.Soap.SiteCodeType.US;

            ReviseFixedPriceItemCall reviseFP = new ReviseFixedPriceItemCall(context);

            ItemType item = new ItemType();
            item.ItemID = listing.ListedItemID;

            NameValueListTypeCollection ItemSpecs = new NameValueListTypeCollection();
            var revisedItemSpecs = ModifyItemSpecific(listing.ItemSpecifics);
            foreach (var i in revisedItemSpecs)
            {
                var n = AddItemSpecifics(i);
                ItemSpecs.Add(n);
            }
            item.ItemSpecifics = ItemSpecs;

            reviseFP.Item = item;

            reviseFP.Execute();
            var r = reviseFP.ApiResponse;
            string msg = r.Ack.ToString();
            if (r.Errors.Count > 0)
            {
                foreach (eBay.Service.Core.Soap.ErrorType e in r.Errors)
                {
                    response.Add(e.LongMessage);
                }
            }
            return response;
        }
        public async static Task RefreshItemSpecifics(UserSettingsView settings, int ID)
        {
            var listing = db.Listings.Where(p => p.ID == ID).SingleOrDefault();
            var sellerListing = await ebayAPIs.GetSingleItem(settings, listing.ItemID);

            var sellerListingdb = db.SellerListings.Find(sellerListing.ItemID);
            sellerListingdb.ItemSpecifics.ForEach(c => c.Updated = DateTime.Now);
            //listing.SellerListing = sellerListingdb;
            await db.SellerListingItemSpecificSave(sellerListing);
            ReviseItemSpecifics(settings, listing);
        }

    }
}