/*
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
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Services.Protocols;

namespace Utility
{
    public class eBayItem
    {
        static dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
        const int _qtyToList = 2;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemID">ebay seller listing id</param>
        /// <returns></returns>
        public static async Task<List<string>> ListingCreateAsync(UserSettingsView settings, string itemID, int storeID)
        {
            var output = new List<string>();
            var token = db.GetToken(storeID);

            var listing = db.ListingGet(itemID, storeID);     // item has to be stored before it can be listed
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
                        listing);
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
            Listing listing)
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

                if (!ItemSpecificExists(listing.SellerListing.ItemSpecifics, "Brand")) { 
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
                spp.PaymentProfileName = "default";

                var srp = new SellerReturnProfileType();
                srp.ReturnProfileName = "mw";

                var ssp = new SellerShippingProfileType();
                ssp.ShippingProfileName = "mw";

                sp.SellerPaymentProfile = spp;
                sp.SellerReturnProfile = srp;
                sp.SellerShippingProfile = ssp;
                item.SellerProfiles = sp;
                // item.SellerProfiles.SellerPaymentProfile = spp;
                // item.SellerProfiles.SellerReturnProfile = srp;
                // item.SellerProfiles.SellerShippingProfile = ssp;

                /*
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
                    Console.WriteLine("=====================================");
                    Console.WriteLine("Add Item Verified");
                    Console.WriteLine("=====================================");
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

        /// <summary>
        /// https://ebaydts.com/eBayKBDetails?KBid=1742
        /// </summary>
        public static string AddFPItemWithVariations(int storeID)
        {
            //create the context
            ApiContext context = new ApiContext();

            //set the User token
            string token = db.GetToken(storeID);
            context.ApiCredential.eBayToken = token;

            //set the server url
            context.SoapApiServerUrl = "https://api.ebay.com/wsapi";

            //enable logging
            context.ApiLogManager = new ApiLogManager();
            context.ApiLogManager.ApiLoggerList.Add(new FileLogger("log.txt", true, true, true));
            context.ApiLogManager.EnableLogging = true;

            //set the version
            context.Version = "673";
            context.Version = "949";
            context.Site = SiteCodeType.US;

            //create the call object
            AddFixedPriceItemCall AddFPItemCall = new AddFixedPriceItemCall(context);

            AddFPItemCall.AutoSetItemUUID = true;

            //create an item object and set the properties
            ItemType item = new ItemType();

            //set the item condition depending on the value from GetCategoryFeatures
            item.ConditionID = 1000; //new with tags

            //Basic properties of a listing
            item.Country = CountryCodeType.US;
            item.Currency = CurrencyCodeType.USD;

            //Track item by SKU
            // item.InventoryTrackingMethod = InventoryTrackingMethodCodeType.SKU;

            //Parent Level SKU
            // item.SKU = "VARPARENT";

            item.Description = "Brand new in box";
            item.Title = "Polo Dress Shirt For Any Occassion";
            item.SubTitle = "Fast Shipping";
            item.ListingDuration = "GTC";
            item.Location = "Multiple locations";

            /*
            item.PaymentMethods = new BuyerPaymentMethodCodeTypeCollection();
            item.PaymentMethods.Add(BuyerPaymentMethodCodeType.PayPal);
            item.PayPalEmailAddress = "test@test.com";
            item.PostalCode = "2001";
            */

            var sp = new SellerProfilesType();

            var spp = new SellerPaymentProfileType();
            spp.PaymentProfileName = "default";

            //Specify Shipping Services
            /*
            item.DispatchTimeMax = 3;
            item.ShippingDetails = new ShippingDetailsType();
            item.ShippingDetails.ShippingServiceOptions = new ShippingServiceOptionsTypeCollection();

            ShippingServiceOptionsType shipservice1 = new ShippingServiceOptionsType();
            shipservice1.ShippingService = "AU_Regular";
            shipservice1.ShippingServicePriority = 1;
            shipservice1.ShippingServiceCost = new AmountType();
            shipservice1.ShippingServiceCost.currencyID = CurrencyCodeType.AUD;
            shipservice1.ShippingServiceCost.Value = 1.0;

            shipservice1.ShippingServiceAdditionalCost = new AmountType();
            shipservice1.ShippingServiceAdditionalCost.currencyID = CurrencyCodeType.AUD;
            shipservice1.ShippingServiceAdditionalCost.Value = 1.0;

            item.ShippingDetails.ShippingServiceOptions.Add(shipservice1);

            ShippingServiceOptionsType shipservice2 = new ShippingServiceOptionsType();
            shipservice2.ShippingService = "AU_Express";
            shipservice2.ShippingServicePriority = 2;
            shipservice2.ShippingServiceCost = new AmountType();
            shipservice2.ShippingServiceCost.currencyID = CurrencyCodeType.AUD;
            shipservice2.ShippingServiceCost.Value = 4.0;

            shipservice2.ShippingServiceAdditionalCost = new AmountType();
            shipservice2.ShippingServiceAdditionalCost.currencyID = CurrencyCodeType.AUD;
            shipservice2.ShippingServiceAdditionalCost.Value = 1.0;

            item.ShippingDetails.ShippingServiceOptions.Add(shipservice2);
            */
            var ssp = new SellerShippingProfileType();
            ssp.ShippingProfileName = "mw";

            //Specify Return Policy
            /*
            item.ReturnPolicy = new ReturnPolicyType();
            item.ReturnPolicy.ReturnsAcceptedOption = "ReturnsAccepted";
            */
            var srp = new SellerReturnProfileType();
            srp.ReturnProfileName = "mw";

            sp.SellerPaymentProfile = spp;
            sp.SellerReturnProfile = srp;
            sp.SellerShippingProfile = ssp;

            item.SellerProfiles = sp;

            item.PrimaryCategory = new CategoryType();
            item.PrimaryCategory.CategoryID = "57991";

            //var pd = new ProductListingDetailsType();
            //pd.UPC = "Does not apply";
            //item.ProductListingDetails = pd;

            var vpd = new VariationProductListingDetailsType();
            vpd.UPC = "Does not apply";

            //Add Item Specifics
            item.ItemSpecifics = new NameValueListTypeCollection();

            NameValueListTypeCollection ItemSpecs = new NameValueListTypeCollection();

            NameValueListType nv1 = new NameValueListType();
            StringCollection valueCol1 = new StringCollection();

            nv1.Name = "Brand";
            valueCol1.Add("Ralph Lauren");
            nv1.Value = valueCol1;
            ItemSpecs.Add(nv1);

            item.ItemSpecifics = ItemSpecs;

            //Specify VariationSpecificsSet
            item.Variations = new VariationsType();

            item.Variations.VariationSpecificsSet = new NameValueListTypeCollection();

            // SIZE
            NameValueListType NVListVS1 = new NameValueListType();
            NVListVS1.Name = "Size";
            StringCollection VSvaluecollection1 = new StringCollection();
            String[] Size = { "XS", "S", "M", "L", "XL" };
            VSvaluecollection1.AddRange(Size);

            NVListVS1.Value = VSvaluecollection1;
            item.Variations.VariationSpecificsSet.Add(NVListVS1);

            // COLOR
            NameValueListType NVListVS2 = new NameValueListType();
            NVListVS2.Name = "Colour";
            StringCollection VSvaluecollection2 = new StringCollection();
            String[] Colour = { "Black", "Blue" };
            VSvaluecollection2.AddRange(Colour);

            NVListVS2.Value = VSvaluecollection2;
            item.Variations.VariationSpecificsSet.Add(NVListVS2);

            // SLEEVE LENGTH
            NameValueListType NVListVS3 = new NameValueListType();
            NVListVS3.Name = "Sleeve Length";
            StringCollection VSvaluecollection3 = new StringCollection();
            String[] SleeveLength = { "Short Sleeve" };
            VSvaluecollection3.AddRange(SleeveLength);

            NVListVS3.Value = VSvaluecollection3;
            item.Variations.VariationSpecificsSet.Add(NVListVS3);

            // SIZE TYPE
            NameValueListType NVListVS4 = new NameValueListType();
            NVListVS4.Name = "Size Type";
            StringCollection VSvaluecollection4 = new StringCollection();
            String[] SizeType = { "Regular" };
            VSvaluecollection4.AddRange(SizeType);

            NVListVS4.Value = VSvaluecollection4;
            item.Variations.VariationSpecificsSet.Add(NVListVS4);

            // DRESS SHIRT SIZE
            NameValueListType NVListVS5 = new NameValueListType();
            NVListVS5.Name = "Dress Shirt Size";
            StringCollection VSvaluecollection5 = new StringCollection();
            String[] DressShirtSize = { "M" };
            VSvaluecollection5.AddRange(DressShirtSize);

            NVListVS5.Value = VSvaluecollection5;
            item.Variations.VariationSpecificsSet.Add(NVListVS5);

            //Specify Variations
            VariationTypeCollection VarCol = new VariationTypeCollection();

            //Variation 1 - Black S
            VariationType var1 = new VariationType();
            //var1.SKU = "VAR1";
            var1.VariationProductListingDetails = vpd;
            var1.Quantity = 0;
            var1.StartPrice = new AmountType();
            var1.StartPrice.currencyID = CurrencyCodeType.USD;
            var1.StartPrice.Value = 35;
            var1.VariationSpecifics = new NameValueListTypeCollection();

            NameValueListType Var1Spec1 = new NameValueListType();
            StringCollection Var1Spec1Valuecoll = new StringCollection();
            Var1Spec1.Name = "Colour";
            Var1Spec1Valuecoll.Add("Black");
            Var1Spec1.Value = Var1Spec1Valuecoll;
            var1.VariationSpecifics.Add(Var1Spec1);

            NameValueListType Var1Spec2 = new NameValueListType();
            StringCollection Var1Spec2Valuecoll = new StringCollection();
            Var1Spec2.Name = "Size";
            Var1Spec2Valuecoll.Add("S");
            Var1Spec2.Value = Var1Spec2Valuecoll;
            var1.VariationSpecifics.Add(Var1Spec2);

            NameValueListType Var1Spec3 = new NameValueListType();
            StringCollection Var1Spec3Valuecoll = new StringCollection();
            Var1Spec3.Name = "Sleeve Length";
            Var1Spec3Valuecoll.Add("Short Sleeve");
            Var1Spec3.Value = Var1Spec3Valuecoll;
            var1.VariationSpecifics.Add(Var1Spec3);

            NameValueListType Var1Spec4 = new NameValueListType();
            StringCollection Var1Spec4Valuecoll = new StringCollection();
            Var1Spec4.Name = "Size Type";
            Var1Spec4Valuecoll.Add("Regular");
            Var1Spec4.Value = Var1Spec4Valuecoll;
            var1.VariationSpecifics.Add(Var1Spec4);

            NameValueListType Var1Spec5 = new NameValueListType();
            StringCollection Var1Spec5Valuecoll = new StringCollection();
            Var1Spec5.Name = "Dress Shirt Size";
            Var1Spec5Valuecoll.Add("M");
            Var1Spec5.Value = Var1Spec5Valuecoll;
            var1.VariationSpecifics.Add(Var1Spec5);

            VarCol.Add(var1);

            //Variation 2 - Black L
            VariationType var2 = new VariationType();
            //var2.SKU = "VAR2";
            var2.VariationProductListingDetails = vpd;
            var2.Quantity = 0;
            var2.StartPrice = new AmountType();
            var2.StartPrice.currencyID = CurrencyCodeType.USD;
            var2.StartPrice.Value = 45;

            var2.VariationSpecifics = new NameValueListTypeCollection();

            NameValueListType Var2Spec1 = new NameValueListType();
            StringCollection Var2Spec1Valuecoll = new StringCollection();
            Var2Spec1.Name = "Colour";
            Var2Spec1Valuecoll.Add("Black");
            Var2Spec1.Value = Var2Spec1Valuecoll;
            var2.VariationSpecifics.Add(Var2Spec1);

            NameValueListType Var2Spec2 = new NameValueListType();
            StringCollection Var2Spec2Valuecoll = new StringCollection();
            Var2Spec2.Name = "Size";
            Var2Spec2Valuecoll.Add("L");
            Var2Spec2.Value = Var2Spec2Valuecoll;
            var2.VariationSpecifics.Add(Var2Spec2);

            NameValueListType Var2Spec3 = new NameValueListType();
            StringCollection Var2Spec3Valuecoll = new StringCollection();
            Var2Spec3.Name = "Sleeve Length";
            Var2Spec3Valuecoll.Add("Short Sleeve");
            Var2Spec3.Value = Var2Spec3Valuecoll;
            var2.VariationSpecifics.Add(Var2Spec3);

            NameValueListType Var2Spec4 = new NameValueListType();
            StringCollection Var2Spec4Valuecoll = new StringCollection();
            Var2Spec4.Name = "Size Type";
            Var2Spec4Valuecoll.Add("Regular");
            Var2Spec4.Value = Var2Spec4Valuecoll;
            var2.VariationSpecifics.Add(Var2Spec4);

            NameValueListType Var2Spec5 = new NameValueListType();
            StringCollection Var2Spec5Valuecoll = new StringCollection();
            Var2Spec5.Name = "Dress Shirt Size";
            Var2Spec5Valuecoll.Add("M");
            Var2Spec5.Value = Var2Spec5Valuecoll;
            var2.VariationSpecifics.Add(Var2Spec5);

            VarCol.Add(var2);

            //Variation 3 - Blue M
            VariationType var3 = new VariationType();
            //var3.SKU = "VAR3";
            var3.VariationProductListingDetails = vpd;
            var3.Quantity = 0;
            var3.StartPrice = new AmountType();
            var3.StartPrice.currencyID = CurrencyCodeType.USD;
            var3.StartPrice.Value = 40;

            var3.VariationSpecifics = new NameValueListTypeCollection();

            NameValueListType Var3Spec1 = new NameValueListType();
            StringCollection Var3Spec1Valuecoll = new StringCollection();
            Var3Spec1.Name = "Colour";
            Var3Spec1Valuecoll.Add("Blue");
            Var3Spec1.Value = Var3Spec1Valuecoll;
            var3.VariationSpecifics.Add(Var3Spec1);

            NameValueListType Var3Spec2 = new NameValueListType();
            StringCollection Var3Spec2Valuecoll = new StringCollection();
            Var3Spec2.Name = "Size";
            Var3Spec2Valuecoll.Add("M");
            Var3Spec2.Value = Var3Spec2Valuecoll;
            var3.VariationSpecifics.Add(Var3Spec2);

            NameValueListType Var3Spec3 = new NameValueListType();
            StringCollection Var3Spec3Valuecoll = new StringCollection();
            Var3Spec3.Name = "Sleeve Length";
            Var3Spec3Valuecoll.Add("Short Sleeve");
            Var3Spec3.Value = Var3Spec3Valuecoll;
            var3.VariationSpecifics.Add(Var3Spec3);

            NameValueListType Var3Spec4 = new NameValueListType();
            StringCollection Var3Spec4Valuecoll = new StringCollection();
            Var3Spec4.Name = "Size Type";
            Var3Spec4Valuecoll.Add("Regular");
            Var3Spec4.Value = Var3Spec4Valuecoll;
            var3.VariationSpecifics.Add(Var3Spec4);

            NameValueListType Var3Spec5 = new NameValueListType();
            StringCollection Var3Spec5Valuecoll = new StringCollection();
            Var3Spec5.Name = "Dress Shirt Size";
            Var3Spec5Valuecoll.Add("M");
            Var3Spec5.Value = Var3Spec5Valuecoll;
            var3.VariationSpecifics.Add(Var3Spec5);

            VarCol.Add(var3);

            //Variation 4 - Blue L
            VariationType var4 = new VariationType();
            //var4.SKU = "VAR4";
            var4.VariationProductListingDetails = vpd;
            var4.Quantity = 0;
            var4.StartPrice = new AmountType();
            var4.StartPrice.currencyID = CurrencyCodeType.USD;
            var4.StartPrice.Value = 45;

            var4.VariationSpecifics = new NameValueListTypeCollection();

            NameValueListType Var4Spec1 = new NameValueListType();
            StringCollection Var4Spec1Valuecoll = new StringCollection();
            Var4Spec1.Name = "Colour";
            Var4Spec1Valuecoll.Add("Blue");
            Var4Spec1.Value = Var4Spec1Valuecoll;
            var4.VariationSpecifics.Add(Var4Spec1);

            NameValueListType Var4Spec2 = new NameValueListType();
            StringCollection Var4Spec2Valuecoll = new StringCollection();
            Var4Spec2.Name = "Size";
            Var4Spec2Valuecoll.Add("L");
            Var4Spec2.Value = Var4Spec2Valuecoll;
            var4.VariationSpecifics.Add(Var4Spec2);

            NameValueListType Var4Spec3 = new NameValueListType();
            StringCollection Var4Spec3Valuecoll = new StringCollection();
            Var4Spec3.Name = "Sleeve Length";
            Var4Spec3Valuecoll.Add("Short Sleeve");
            Var4Spec3.Value = Var4Spec3Valuecoll;
            var4.VariationSpecifics.Add(Var4Spec3);

            NameValueListType Var4Spec4 = new NameValueListType();
            StringCollection Var4Spec4Valuecoll = new StringCollection();
            Var4Spec4.Name = "Size Type";
            Var4Spec4Valuecoll.Add("Regular");
            Var4Spec4.Value = Var4Spec4Valuecoll;
            var4.VariationSpecifics.Add(Var4Spec4);

            NameValueListType Var4Spec5 = new NameValueListType();
            StringCollection Var4Spec5Valuecoll = new StringCollection();
            Var4Spec5.Name = "Dress Shirt Size";
            Var4Spec5Valuecoll.Add("M");
            Var4Spec5.Value = Var4Spec5Valuecoll;
            var4.VariationSpecifics.Add(Var4Spec5);

            VarCol.Add(var4);

            //Add Variation Specific Pictures
            item.Variations.Pictures = new PicturesTypeCollection();

            PicturesType pic = new PicturesType();
            pic.VariationSpecificName = "Colour";
            pic.VariationSpecificPictureSet = new VariationSpecificPictureSetTypeCollection();

            VariationSpecificPictureSetType VarPicSet1 = new VariationSpecificPictureSetType();
            VarPicSet1.VariationSpecificValue = "Black";
            StringCollection PicURLVarPicSet1 = new StringCollection();
            PicURLVarPicSet1.Add("https://oldnavy.gap.com/webcontent/0014/436/371/cn14436371.jpg");
            VarPicSet1.PictureURL = PicURLVarPicSet1;

            pic.VariationSpecificPictureSet.Add(VarPicSet1);

            VariationSpecificPictureSetType VarPicSet2 = new VariationSpecificPictureSetType();
            VarPicSet2.VariationSpecificValue = "Blue";
            StringCollection PicURLVarPicSet2 = new StringCollection();
            PicURLVarPicSet2.Add("https://oldnavy.gap.com/webcontent/0014/436/365/cn14436365.jpg");
            VarPicSet2.PictureURL = PicURLVarPicSet2;

            pic.VariationSpecificPictureSet.Add(VarPicSet2);

            item.Variations.Pictures.Add(pic);

            item.Variations.Variation = VarCol;

            //Add item level pictures
            item.PictureDetails = new PictureDetailsType();
            item.PictureDetails.PictureURL = new StringCollection();
            item.PictureDetails.PictureURL.Add("https://oldnavy.gap.com/webcontent/0014/436/365/cn14436365.jpg");
            item.PictureDetails.PhotoDisplay = PhotoDisplayCodeType.SuperSize;
            item.PictureDetails.PhotoDisplaySpecified = true;

            AddFPItemCall.Item = item;

            //set the item and make the call
            AddFPItemCall.Execute();

            string result = AddFPItemCall.ApiResponse.Ack + " " + AddFPItemCall.ApiResponse.ItemID;
            return result;
        }

        protected static eBay.Service.Core.Soap.NameValueListType AddItemSpecifics(SellerListingItemSpecific item)
        {
            var itemSpecs = new NameValueListTypeCollection();
            var nv2 = new eBay.Service.Core.Soap.NameValueListType();
            StringCollection valueCol2 = new StringCollection();

            nv2.Name = item.ItemName;
            valueCol2.Add(item.ItemValue);
            nv2.Value = valueCol2;

            return nv2;
        }
        protected static List<SellerListingItemSpecific> ModifyItemSpecific(List<SellerListingItemSpecific> itemSpecifics)
        {
            var specifics = new List<SellerListingItemSpecific>();
            foreach(var s in itemSpecifics)
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
            if (name == "Return shipping will be paid by")
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


        public static string EndFixedPriceItem(Listing listing)
        {
            //create the context
            ApiContext context = new ApiContext();

            //set the User token
            string token = db.GetToken(listing.StoreID);
            context.ApiCredential.eBayToken = token;

            //set the server url
            //string endpoint = AppSettingsHelper.Endpoint;
            //context.SoapApiServerUrl = endpoint;

            //enable logging
            context.ApiLogManager = new ApiLogManager();
            context.ApiLogManager.ApiLoggerList.Add(new FileLogger("log.txt", true, true, true));
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
            //create the context
            ApiContext context = new ApiContext();

            //set the User token
            context.ApiCredential.eBayToken = token;

            //set the server url
            //string endpoint = AppSettingsHelper.Endpoint;
            //context.SoapApiServerUrl = endpoint;

            //enable logging
            context.ApiLogManager = new ApiLogManager();
            context.ApiLogManager.ApiLoggerList.Add(new FileLogger("log.txt", true, true, true));
            context.ApiLogManager.EnableLogging = true;

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
            return response;
        }

        protected static decimal wmBreakEvenPrice(decimal supplierPrice, decimal minFreeShipping, decimal shipping)
        {
            if (supplierPrice < minFreeShipping)
            {
                supplierPrice += shipping;
            }
            decimal p = (supplierPrice + 0.30m) / (1m - 0.029m - 0.0915m);
            return p;
        }

        /// <summary>
        /// https://community.ebay.com/t5/Selling/Excel-Spreadsheet-formula-to-break-even-with-eBay-sales/qaq-p/23249463
        /// Markup b/e price by pctProfit percent
        /// </summary>
        /// <param name="supplierPrice"></param>
        /// <returns></returns>
        public static decimal wmNewPrice(decimal supplierPrice, double pctProfit)
        {
            decimal breakeven = wmBreakEvenPrice(supplierPrice, 35.0m, 6.0m);
            return breakeven * (1m + ((decimal)pctProfit * 0.01m));
        }
    }
}