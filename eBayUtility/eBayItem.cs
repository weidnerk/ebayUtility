﻿/*
 * Listing functions.
 * 
 * This file is heavily dependent on the .NET SDK (References/eBay.Service)
 * but different in that calls go through the eBayAPIInterfaceService object.
 * 
 * 
 */
using dsmodels;
using eBay.Service.Call;
using eBay.Service.Core.Sdk;
using eBay.Service.Core.Soap;
using eBay.Service.Util;
using eBayUtility;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Services.Protocols;

namespace Utility
{
    public class eBayItem
    {
        static dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
        const int _qtyToList = 2;
        const string _logfile = "log.txt";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemID">ebay seller listing id</param>
        /// <returns></returns>
        public static async Task<List<string>> ListingCreateAsync(UserSettingsView settings, int listingID, string shippingProfile, string returnProfile, string paymentProfile)
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
                    string verifyItemID = eBayItem.VerifyAddItemRequest(settings, listing.ListingTitle,
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
                    // at this point, 'output' will be populated with errors if any occurred

                    if (!string.IsNullOrEmpty(verifyItemID))
                    {
                        output.Insert(0, verifyItemID);
                        output.Insert(0, "Listed: YES");
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
                    if (output.Count > 0)
                    {
                        response = FlattenList(output);
                    }
                    await db.ListedItemIDUpdate(listing, listing.ListedItemID, settings.UserID, true, response, updated: DateTime.Now);
                    output.Insert(0, listing.ListedItemID);
                }
            }
            return output;
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

                if (!ItemSpecificExists(listing.SellerListing.ItemSpecifics, "Brand"))
                {
                    nv1.Name = "Brand";
                    valueCol1.Add("Unbranded");
                    nv1.Value = valueCol1;
                    ItemSpecs.Add(nv1);
                }
                if (!ItemSpecificExists(listing.SellerListing.ItemSpecifics, "MPN"))
                {
                    nv2.Name = "MPN";
                    valueCol2.Add("Does Not Apply");
                    nv2.Value = valueCol2;
                    ItemSpecs.Add(nv2);
                }

                var revisedItemSpecs = ModifyItemSpecific(listing.SellerListing.ItemSpecifics);
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

        public static eBay.Service.Core.Soap.NameValueListType AddItemSpecifics(SellerListingItemSpecific item)
        {
            var nv2 = new eBay.Service.Core.Soap.NameValueListType();
            StringCollection valueCol2 = new StringCollection();

            nv2.Name = item.ItemName;
            valueCol2.Add(item.ItemValue);
            nv2.Value = valueCol2;

            return nv2;
        }
        public static List<SellerListingItemSpecific> ModifyItemSpecific(List<SellerListingItemSpecific> itemSpecifics)
        {
            var specifics = new List<SellerListingItemSpecific>();
            foreach (var s in itemSpecifics)
            {
                if (!OmitSpecific(s.ItemName))
                {
                    specifics.Add(s);
                }
            }
            return specifics;
        }
        protected static bool ItemSpecificExists(List<SellerListingItemSpecific> itemSpecifics, string itemName)
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


        public static string EndFixedPriceItem(UserSettingsView settings, Listing listing)
        {
            //create the context
            ApiContext context = new ApiContext();

            //set the User token
            string token = db.GetToken(settings);
            context.ApiCredential.eBayToken = token;

            //set the server url

            //enable logging
            context.ApiLogManager = new ApiLogManager();
            context.ApiLogManager.ApiLoggerList.Add(new FileLogger("logebay.txt", true, true, true));
            context.ApiLogManager.EnableLogging = true;

            //set the version
            context.Version = "817";
            context.Site = eBay.Service.Core.Soap.SiteCodeType.US;

            EndFixedPriceItemCall endFP = new EndFixedPriceItemCall(context);

            endFP.ItemID = listing.ListedItemID;
            endFP.EndingReason = EndReasonCodeType.NotAvailable;

            endFP.Execute();
            string result = endFP.ApiResponse.Ack + " Ended ItemID " + endFP.ItemID;
            return result;
        }

        // use this for itemspecifics:
        // https://ebaydts.com/eBayKBDetails?KBid=1647
        //
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
                context.ApiLogManager = new ApiLogManager();
                context.ApiLogManager.ApiLoggerList.Add(new FileLogger("logebay.txt", true, true, true));
                /*
                 * PLEASE NOTE:
                 * Long time issue of finishing the listing revise but then navigating to another page is slow.
                 * Turn off logging fixes this.  Not sure what ebay logger is doing
                 */
                context.ApiLogManager.EnableLogging = false;

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
            context.ApiLogManager.ApiLoggerList.Add(new FileLogger("logebay.txt", true, true, true));
            context.ApiLogManager.EnableLogging = true;

            //set the version
            context.Version = "817";
            context.Site = eBay.Service.Core.Soap.SiteCodeType.US;

            ReviseFixedPriceItemCall reviseFP = new ReviseFixedPriceItemCall(context);

            ItemType item = new ItemType();
            item.ItemID = listing.ListedItemID;

            NameValueListTypeCollection ItemSpecs = new NameValueListTypeCollection();
            var revisedItemSpecs = ModifyItemSpecific(listing.SellerListing.ItemSpecifics);
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
            var sellerListing = await ebayAPIs.GetSingleItem(settings, listing.SellerListing.ItemID);

            var sellerListingdb = db.SellerListings.Find(sellerListing.ItemID);
            sellerListingdb.ItemSpecifics.ForEach(c => c.Updated = DateTime.Now);
            listing.SellerListing = sellerListingdb;
            await db.SellerListingItemSpecificSave(sellerListing);
            ReviseItemSpecifics(settings, listing);
        }

    }
}