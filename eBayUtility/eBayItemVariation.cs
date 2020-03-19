/*
 * 
 * Test code for working with eBay variations.
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
    public class eBayItemVariation
    {
        static dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();

        /// <summary>
        /// TEST CODE
        /// https://ebaydts.com/eBayKBDetails?KBid=1742
        /// </summary>
        public static string AddFPItemWithVariations_closet(UserSettingsView settings, int storeID)
        {
            //create the context
            ApiContext context = new ApiContext();

            //set the User token
            string token = db.GetToken(settings);
            context.ApiCredential.eBayToken = token;

            //set the server url
            context.SoapApiServerUrl = "https://api.ebay.com/wsapi";

            //enable logging
            context.ApiLogManager = new ApiLogManager();
            context.ApiLogManager.ApiLoggerList.Add(new FileLogger("logebay.txt", true, true, true));
            context.ApiLogManager.EnableLogging = true;

            //set the version
            context.Version = "673";
            context.Version = "949";
            context.Site = SiteCodeType.US;

            //create the call object
            AddFixedPriceItemCall AddFPItemCall = new AddFixedPriceItemCall(context);

            // 02.02.2020 was getting listed with this but i don't know what it is so commenting it out.
            //AddFPItemCall.AutoSetItemUUID = true;

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

            item.Description = "NEW WITH TAGS";
            item.Title = "Tall Storage Cabinet Kitchen Pantry Cupboard Organizer Furniture";

            // called eBay and they said i was getting charged for subtitle - I never use it so don't use it
            // 02.02/2020 Bonnie credited me back
            // item.SubTitle = "Fast Shipping";

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
            item.PrimaryCategory.CategoryID = "20487"; // for the closet

            //var pd = new ProductListingDetailsType();
            //pd.UPC = "Does not apply";
            //item.ProductListingDetails = pd;

            var vpd = new VariationProductListingDetailsType();
            vpd.UPC = "Does not apply";

            //Add Item Specifics
            item.ItemSpecifics = new NameValueListTypeCollection();

            #region item_specs
            NameValueListTypeCollection ItemSpecs = new NameValueListTypeCollection();

            NameValueListType nv1 = new NameValueListType();
            StringCollection valueCol1 = new StringCollection();

            nv1.Name = "Brand";
            valueCol1.Add("Mainstays");
            nv1.Value = valueCol1;
            ItemSpecs.Add(nv1);

            NameValueListType nv2 = new NameValueListType();
            StringCollection valueCol2 = new StringCollection();

            nv2.Name = "Item Width (Interior)";
            valueCol2.Add("80");
            nv2.Value = valueCol2;
            ItemSpecs.Add(nv2);

            NameValueListType nv3 = new NameValueListType();
            StringCollection valueCol3 = new StringCollection();

            nv3.Name = "Product Line";
            valueCol3.Add("Cabinet");
            nv3.Value = valueCol3;
            ItemSpecs.Add(nv3);

            NameValueListType nv4 = new NameValueListType();
            StringCollection valueCol4 = new StringCollection();

            nv4.Name = "MPN";
            valueCol4.Add("7224301PCOM");
            nv4.Value = valueCol4;
            ItemSpecs.Add(nv4);

            NameValueListType nv5 = new NameValueListType();
            StringCollection valueCol5 = new StringCollection();

            nv5.Name = "Item Length (Interior)";
            valueCol5.Add("N/A");
            nv5.Value = valueCol5;
            ItemSpecs.Add(nv5);

            NameValueListType nv6 = new NameValueListType();
            StringCollection valueCol6 = new StringCollection();

            nv6.Name = "Item Height";
            valueCol6.Add("60");
            nv6.Value = valueCol6;
            ItemSpecs.Add(nv6);

            NameValueListType nv7 = new NameValueListType();
            StringCollection valueCol7 = new StringCollection();

            nv7.Name = "Number of Shelves";
            valueCol7.Add("3");
            nv7.Value = valueCol7;
            ItemSpecs.Add(nv7);

            NameValueListType nv8 = new NameValueListType();
            StringCollection valueCol8 = new StringCollection();

            nv8.Name = "Item Weight";
            valueCol8.Add("60 lbs");
            nv8.Value = valueCol8;
            ItemSpecs.Add(nv8);

            NameValueListType nv9 = new NameValueListType();
            StringCollection valueCol9 = new StringCollection();

            nv9.Name = "Mounting";
            valueCol9.Add("free standing");
            nv9.Value = valueCol9;
            ItemSpecs.Add(nv9);

            NameValueListType nv10 = new NameValueListType();
            StringCollection valueCol10 = new StringCollection();

            nv10.Name = "Type";
            valueCol10.Add("Storage Cabinet");
            nv10.Value = valueCol10;
            ItemSpecs.Add(nv10);

            NameValueListType nv11 = new NameValueListType();
            StringCollection valueCol11 = new StringCollection();

            nv11.Name = "Assembly Required";
            valueCol11.Add("Yes");
            nv11.Value = valueCol11;
            ItemSpecs.Add(nv11);

            NameValueListType nv12 = new NameValueListType();
            StringCollection valueCol12 = new StringCollection();

            nv12.Name = "Material";
            valueCol12.Add("MDF/Chipboard");
            nv12.Value = valueCol12;
            ItemSpecs.Add(nv12);

            NameValueListType nv13 = new NameValueListType();
            StringCollection valueCol13 = new StringCollection();

            nv13.Name = "Additional Parts Required";
            valueCol13.Add("No");
            nv13.Value = valueCol13;
            ItemSpecs.Add(nv13);

            NameValueListType nv14 = new NameValueListType();
            StringCollection valueCol14 = new StringCollection();

            nv14.Name = "Item Height (Interior)";
            valueCol14.Add("N/A");
            nv14.Value = valueCol14;
            ItemSpecs.Add(nv14);

            NameValueListType nv15 = new NameValueListType();
            StringCollection valueCol15 = new StringCollection();

            nv15.Name = "Department";
            valueCol15.Add("Adults");
            nv15.Value = valueCol15;
            ItemSpecs.Add(nv15);

            NameValueListType nv16 = new NameValueListType();
            StringCollection valueCol16 = new StringCollection();

            nv16.Name = "Model";
            valueCol16.Add("7224015PCOM");
            nv16.Value = valueCol16;
            ItemSpecs.Add(nv16);

            NameValueListType nv17 = new NameValueListType();
            StringCollection valueCol17 = new StringCollection();

            nv17.Name = "Item Width";
            valueCol17.Add("21.31");
            nv17.Value = valueCol17;
            ItemSpecs.Add(nv17);

            NameValueListType nv18 = new NameValueListType();
            StringCollection valueCol18 = new StringCollection();

            nv18.Name = "Item Length";
            valueCol18.Add("15");
            nv18.Value = valueCol18;
            ItemSpecs.Add(nv18);

            item.ItemSpecifics = ItemSpecs;
            #endregion

            //Specify VariationSpecificsSet
            item.Variations = new VariationsType();

            item.Variations.VariationSpecificsSet = new NameValueListTypeCollection();

            // COLOR
            /*
            NameValueListType NVListVS1 = new NameValueListType();
            NVListVS1.Name = "Color";
            StringCollection VSvaluecollection1 = new StringCollection();
            String[] Size = { "Brown", "White" };
            VSvaluecollection1.AddRange(Size);

            NVListVS1.Value = VSvaluecollection1;
            */
            item.Variations.VariationSpecificsSet.Add(CreateSpecSet("Brown", "White"));

            //Specify Variations
            VariationTypeCollection VarCol = new VariationTypeCollection();

            //Variation 1 - Black S
            VariationType var1 = new VariationType();
            //var1.SKU = "VAR1";
            var1.VariationProductListingDetails = vpd;
            var1.Quantity = 1;
            var1.StartPrice = new AmountType();
            var1.StartPrice.currencyID = CurrencyCodeType.USD;
            var1.StartPrice.Value = 1095;
            var1.VariationSpecifics = new NameValueListTypeCollection();

            NameValueListType Var1Spec1 = new NameValueListType();
            StringCollection Var1Spec1Valuecoll = new StringCollection();
            Var1Spec1.Name = "Color";
            Var1Spec1Valuecoll.Add("Brown");
            Var1Spec1.Value = Var1Spec1Valuecoll;
            var1.VariationSpecifics.Add(Var1Spec1);

            VarCol.Add(var1);

            //Variation 2 - Black L
            VariationType var2 = new VariationType();
            //var2.SKU = "VAR2";
            var2.VariationProductListingDetails = vpd;
            var2.Quantity = 1;
            var2.StartPrice = new AmountType();
            var2.StartPrice.currencyID = CurrencyCodeType.USD;
            var2.StartPrice.Value = 1085;

            var2.VariationSpecifics = new NameValueListTypeCollection();

            NameValueListType Var2Spec1 = new NameValueListType();
            StringCollection Var2Spec1Valuecoll = new StringCollection();
            Var2Spec1.Name = "Color";
            Var2Spec1Valuecoll.Add("White");
            Var2Spec1.Value = Var2Spec1Valuecoll;
            var2.VariationSpecifics.Add(Var2Spec1);

            VarCol.Add(var2);

            //Add Variation Specific Pictures
            item.Variations.Pictures = new PicturesTypeCollection();

            PicturesType pic = new PicturesType();
            pic.VariationSpecificName = "Color";
            pic.VariationSpecificPictureSet = new VariationSpecificPictureSetTypeCollection();

            VariationSpecificPictureSetType VarPicSet1 = new VariationSpecificPictureSetType();
            VarPicSet1.VariationSpecificValue = "Brown";
            StringCollection PicURLVarPicSet1 = new StringCollection();
            PicURLVarPicSet1.Add("https://secure.img1-fg.wfcdn.com/im/30553166/resize-h800%5Ecompr-r85/8612/86125047/Mapleville+Armoire.jpg");
            VarPicSet1.PictureURL = PicURLVarPicSet1;

            pic.VariationSpecificPictureSet.Add(VarPicSet1);

            VariationSpecificPictureSetType VarPicSet2 = new VariationSpecificPictureSetType();
            VarPicSet2.VariationSpecificValue = "White";
            StringCollection PicURLVarPicSet2 = new StringCollection();
            PicURLVarPicSet2.Add("https://images.containerstore.com/catalogimages/334817/EL_17_10072941-elfa-White_Birch_V1_R.jpg?width=1200&height=1200&align=center");
            VarPicSet2.PictureURL = PicURLVarPicSet2;

            pic.VariationSpecificPictureSet.Add(VarPicSet2);

            item.Variations.Pictures.Add(pic);

            item.Variations.Variation = VarCol;

            //Add item level pictures
            item.PictureDetails = new PictureDetailsType();
            item.PictureDetails.PictureURL = new StringCollection();
            item.PictureDetails.PictureURL.Add("https://secure.img1-fg.wfcdn.com/im/30553166/resize-h800%5Ecompr-r85/8612/86125047/Mapleville+Armoire.jpg");
            item.PictureDetails.PhotoDisplay = PhotoDisplayCodeType.SuperSize;
            item.PictureDetails.PhotoDisplaySpecified = true;

            AddFPItemCall.Item = item;

            //set the item and make the call
            AddFPItemCall.Execute();

            string result = AddFPItemCall.ApiResponse.Ack + " " + AddFPItemCall.ApiResponse.ItemID;
            return result;
        }

        /// <summary>
        /// TEST CODE
        /// Received an error bcs the name I was using for the variation spec was also included as a item specific.
        /// In this case I was using "Color" as the variation spec but then I checked what the seller was using and he used "Choose Color".
        /// I did the same and then it listed.
        /// </summary>
        /// <param name="storeID"></param>
        /// <param name="sellerListing"></param>
        /// <returns></returns>
        public static string AddFPItemWithVariations_microwave(UserSettingsView settings, int storeID, SellerListing sellerListing)
        {
            //create the context
            ApiContext context = new ApiContext();

            //set the User token
            string token = db.GetToken(settings);
            context.ApiCredential.eBayToken = token;

            //set the server url
            context.SoapApiServerUrl = "https://api.ebay.com/wsapi";

            //enable logging
            context.ApiLogManager = new ApiLogManager();
            context.ApiLogManager.ApiLoggerList.Add(new FileLogger("logebay.txt", true, true, true));
            context.ApiLogManager.EnableLogging = true;

            //set the version
            context.Version = "673";
            context.Version = "949";
            context.Site = SiteCodeType.US;

            //create the call object
            AddFixedPriceItemCall AddFPItemCall = new AddFixedPriceItemCall(context);

            // 02.02.2020 was getting listed with this but i don't know what it is so commenting it out.
            //AddFPItemCall.AutoSetItemUUID = true;

            //create an item object and set the properties
            ItemType item = new ItemType();

            //set the item condition depending on the value from GetCategoryFeatures
            item.ConditionID = 1000; //new with tags

            //Basic properties of a listing
            item.Country = CountryCodeType.US;
            item.Currency = CurrencyCodeType.USD;

            //Track item by SKU
            //item.InventoryTrackingMethod = InventoryTrackingMethodCodeType.SKU;

            //Parent Level SKU
            // item.SKU = "VARPARENT";

            item.Description = "NEW WITH TAGS";
            item.Title = "Low Profile Microwave Oven RV Dorm Mini Small Best Compact Kitchen Countertop";

            // called eBay and they said i was getting charged for subtitle - I never use it so don't use it
            // 02.02/2020 Bonnie credited me back
            // item.SubTitle = "Fast Shipping";

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
            item.PrimaryCategory.CategoryID = sellerListing.PrimaryCategoryID;

            //var pd = new ProductListingDetailsType();
            //pd.UPC = "Does not apply";
            //item.ProductListingDetails = pd;

            var vpd = new VariationProductListingDetailsType();
            vpd.UPC = "Does not apply";

            //Add Item Specifics
            NameValueListTypeCollection ItemSpecs = new NameValueListTypeCollection();
            var revisedItemSpecs = Utility.eBayItem.ModifyItemSpecific(sellerListing.ItemSpecifics);
            foreach (var i in revisedItemSpecs)
            {
                var n = Utility.eBayItem.AddItemSpecifics(i);
                ItemSpecs.Add(n);
            }
            item.ItemSpecifics = ItemSpecs;


            //Specify VariationSpecificsSet
            item.Variations = new VariationsType();

            item.Variations.VariationSpecificsSet = new NameValueListTypeCollection();

            // COLOR
            /*
            NameValueListType NVListVS1 = new NameValueListType();
            NVListVS1.Name = "Color";
            StringCollection VSvaluecollection1 = new StringCollection();
            String[] Size = { "Brown", "White" };
            VSvaluecollection1.AddRange(Size);

            NVListVS1.Value = VSvaluecollection1;
            */
            string varSpecName = null;
            if (sellerListing.Variations.Count > 0)
            {
                varSpecName = GetVariationSpec(sellerListing.Variations[0]);
            }

            item.Variations.VariationSpecificsSet.Add(CreateSpecSet(varSpecName, "Black", "White"));

            //Specify Variations
            VariationTypeCollection VarCol = new VariationTypeCollection();

            //Variation 1 - Black S
            VariationType var1 = new VariationType();
            //var1.SKU = "VAR1";
            var1.VariationProductListingDetails = vpd;
            var1.Quantity = 1;
            var1.StartPrice = new AmountType();
            var1.StartPrice.currencyID = CurrencyCodeType.USD;
            var1.StartPrice.Value = 1095;
            var1.VariationSpecifics = new NameValueListTypeCollection();

            NameValueListType Var1Spec1 = new NameValueListType();
            StringCollection Var1Spec1Valuecoll = new StringCollection();
            Var1Spec1.Name = varSpecName;
            Var1Spec1Valuecoll.Add("Black");
            Var1Spec1.Value = Var1Spec1Valuecoll;
            var1.VariationSpecifics.Add(Var1Spec1);

            VarCol.Add(var1);

            //Variation 2 - Black L
            VariationType var2 = new VariationType();
            //var2.SKU = "VAR2";
            var2.VariationProductListingDetails = vpd;
            var2.Quantity = 1;
            var2.StartPrice = new AmountType();
            var2.StartPrice.currencyID = CurrencyCodeType.USD;
            var2.StartPrice.Value = 1085;

            var2.VariationSpecifics = new NameValueListTypeCollection();

            NameValueListType Var2Spec1 = new NameValueListType();
            StringCollection Var2Spec1Valuecoll = new StringCollection();
            Var2Spec1.Name = varSpecName;
            Var2Spec1Valuecoll.Add("White");
            Var2Spec1.Value = Var2Spec1Valuecoll;
            var2.VariationSpecifics.Add(Var2Spec1);

            VarCol.Add(var2);

            //Add Variation Specific Pictures
            item.Variations.Pictures = new PicturesTypeCollection();

            PicturesType pic = new PicturesType();
            pic.VariationSpecificName = varSpecName;
            pic.VariationSpecificPictureSet = new VariationSpecificPictureSetTypeCollection();

            VariationSpecificPictureSetType VarPicSet1 = new VariationSpecificPictureSetType();
            VarPicSet1.VariationSpecificValue = "White";
            StringCollection PicURLVarPicSet1 = new StringCollection();
            PicURLVarPicSet1.Add("https://pisces.bbystatic.com/image2/BestBuy_US/images/products/6195/6195413_sd.jpg");
            VarPicSet1.PictureURL = PicURLVarPicSet1;

            pic.VariationSpecificPictureSet.Add(VarPicSet1);

            VariationSpecificPictureSetType VarPicSet2 = new VariationSpecificPictureSetType();
            VarPicSet2.VariationSpecificValue = "Black";
            StringCollection PicURLVarPicSet2 = new StringCollection();
            PicURLVarPicSet2.Add("https://pisces.bbystatic.com/image2/BestBuy_US/images/products/6195/6195411_sd.jpg");
            VarPicSet2.PictureURL = PicURLVarPicSet2;

            pic.VariationSpecificPictureSet.Add(VarPicSet2);

            item.Variations.Pictures.Add(pic);

            item.Variations.Variation = VarCol;

            //Add item level pictures
            item.PictureDetails = new PictureDetailsType();
            item.PictureDetails.PictureURL = new StringCollection();
            item.PictureDetails.PictureURL.Add("https://pisces.bbystatic.com/image2/BestBuy_US/images/products/6195/6195413_sd.jpg");
            item.PictureDetails.PhotoDisplay = PhotoDisplayCodeType.SuperSize;
            item.PictureDetails.PhotoDisplaySpecified = true;

            AddFPItemCall.Item = item;

            //set the item and make the call
            AddFPItemCall.Execute();

            string result = AddFPItemCall.ApiResponse.Ack + " " + AddFPItemCall.ApiResponse.ItemID;
            return result;
        }

        /// <summary>
        /// TEST CODE
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="storeID"></param>
        /// <param name="sellerListing"></param>
        /// <returns></returns>
        public static string AddFPItemWithVariations_cutlery(UserSettingsView settings, int storeID, SellerListing sellerListing)
        {
            //create the context
            ApiContext context = new ApiContext();

            //set the User token
            string token = db.GetToken(settings);
            context.ApiCredential.eBayToken = token;

            //set the server url
            context.SoapApiServerUrl = "https://api.ebay.com/wsapi";

            //enable logging
            context.ApiLogManager = new ApiLogManager();
            context.ApiLogManager.ApiLoggerList.Add(new FileLogger("logebay.txt", true, true, true));
            context.ApiLogManager.EnableLogging = true;

            //set the version
            context.Version = "673";
            context.Version = "949";
            context.Site = SiteCodeType.US;

            //create the call object
            AddFixedPriceItemCall AddFPItemCall = new AddFixedPriceItemCall(context);

            // 02.02.2020 was getting listed with this but i don't know what it is so commenting it out.
            //AddFPItemCall.AutoSetItemUUID = true;

            //create an item object and set the properties
            ItemType item = new ItemType();

            //set the item condition depending on the value from GetCategoryFeatures
            item.ConditionID = 1000; //new with tags

            //Basic properties of a listing
            item.Country = CountryCodeType.US;
            item.Currency = CurrencyCodeType.USD;

            //Track item by SKU
            //item.InventoryTrackingMethod = InventoryTrackingMethodCodeType.SKU;

            //Parent Level SKU
            // item.SKU = "VARPARENT";

            item.Description = "NEW WITH TAGS";
            item.Title = "The Pioneer Woman Cowboy Rustic Cutlery 14-Piece Kitchen Tools";

            // called eBay and they said i was getting charged for subtitle - I never use it so don't use it
            // 02.02/2020 Bonnie credited me back
            // item.SubTitle = "Fast Shipping";

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

            var ssp = new SellerShippingProfileType();
            ssp.ShippingProfileName = "mw";

            var srp = new SellerReturnProfileType();
            srp.ReturnProfileName = "mw";

            sp.SellerPaymentProfile = spp;
            sp.SellerReturnProfile = srp;
            sp.SellerShippingProfile = ssp;

            item.SellerProfiles = sp;

            item.PrimaryCategory = new CategoryType();
            item.PrimaryCategory.CategoryID = sellerListing.PrimaryCategoryID;

            //var pd = new ProductListingDetailsType();
            //pd.UPC = "Does not apply";
            //item.ProductListingDetails = pd;

            var vpd = new VariationProductListingDetailsType();
            vpd.UPC = "Does not apply";

            //Add Item Specifics
            NameValueListTypeCollection ItemSpecs = new NameValueListTypeCollection();
            var revisedItemSpecs = Utility.eBayItem.ModifyItemSpecific(sellerListing.ItemSpecifics);
            foreach (var i in revisedItemSpecs)
            {
                var n = Utility.eBayItem.AddItemSpecifics(i);
                ItemSpecs.Add(n);
            }
            item.ItemSpecifics = ItemSpecs;

            //Specify VariationSpecificsSet
            item.Variations = new VariationsType();

            item.Variations.VariationSpecificsSet = new NameValueListTypeCollection();

            // COLOR
            /*
            NameValueListType NVListVS1 = new NameValueListType();
            NVListVS1.Name = "Color";
            StringCollection VSvaluecollection1 = new StringCollection();
            String[] Size = { "Brown", "White" };
            VSvaluecollection1.AddRange(Size);

            NVListVS1.Value = VSvaluecollection1;
            */
            string varSpecName = null;
            if (sellerListing.Variations.Count > 0)
            {
                varSpecName = GetVariationSpec(sellerListing.Variations[0]);
            }
            item.Variations.VariationSpecificsSet.Add(CreateSpecSet(varSpecName, "Black", "White"));

            //Specify Variations
            VariationTypeCollection VarCol = new VariationTypeCollection();

            //Variation 1 - Black S
            VariationType var1 = new VariationType();
            //var1.SKU = "VAR1";
            var1.VariationProductListingDetails = vpd;
            var1.Quantity = 1;
            var1.StartPrice = new AmountType();
            var1.StartPrice.currencyID = CurrencyCodeType.USD;
            var1.StartPrice.Value = 1095;
            var1.VariationSpecifics = new NameValueListTypeCollection();

            NameValueListType Var1Spec1 = new NameValueListType();
            StringCollection Var1Spec1Valuecoll = new StringCollection();
            Var1Spec1.Name = varSpecName;
            Var1Spec1Valuecoll.Add("Black");
            Var1Spec1.Value = Var1Spec1Valuecoll;
            var1.VariationSpecifics.Add(Var1Spec1);

            VarCol.Add(var1);

            //Variation 2 - Black L
            VariationType var2 = new VariationType();
            //var2.SKU = "VAR2";
            var2.VariationProductListingDetails = vpd;
            var2.Quantity = 1;
            var2.StartPrice = new AmountType();
            var2.StartPrice.currencyID = CurrencyCodeType.USD;
            var2.StartPrice.Value = 1085;

            var2.VariationSpecifics = new NameValueListTypeCollection();

            NameValueListType Var2Spec1 = new NameValueListType();
            StringCollection Var2Spec1Valuecoll = new StringCollection();
            Var2Spec1.Name = varSpecName;
            Var2Spec1Valuecoll.Add("White");
            Var2Spec1.Value = Var2Spec1Valuecoll;
            var2.VariationSpecifics.Add(Var2Spec1);

            VarCol.Add(var2);

            //Add Variation Specific Pictures
            item.Variations.Pictures = new PicturesTypeCollection();

            PicturesType pic = new PicturesType();
            pic.VariationSpecificName = varSpecName;
            pic.VariationSpecificPictureSet = new VariationSpecificPictureSetTypeCollection();

            VariationSpecificPictureSetType VarPicSet1 = new VariationSpecificPictureSetType();
            VarPicSet1.VariationSpecificValue = "Black";
            StringCollection PicURLVarPicSet1 = new StringCollection();
            PicURLVarPicSet1.Add("https://cfcdn.zulily.com/images/cache/product//194736/zu40629302_main_tm1469564926.jpg");
            VarPicSet1.PictureURL = PicURLVarPicSet1;

            pic.VariationSpecificPictureSet.Add(VarPicSet1);

            VariationSpecificPictureSetType VarPicSet2 = new VariationSpecificPictureSetType();
            VarPicSet2.VariationSpecificValue = "White";
            StringCollection PicURLVarPicSet2 = new StringCollection();
            PicURLVarPicSet2.Add("https://secure.img1-fg.wfcdn.com/im/39048788/resize-h800%5Ecompr-r85/1752/17528312/Patterson+Flatware+Set.jpg");
            VarPicSet2.PictureURL = PicURLVarPicSet2;

            pic.VariationSpecificPictureSet.Add(VarPicSet2);

            item.Variations.Pictures.Add(pic);

            item.Variations.Variation = VarCol;

            //Add item level pictures
            item.PictureDetails = new PictureDetailsType();
            item.PictureDetails.PictureURL = new StringCollection();
            item.PictureDetails.PictureURL.Add("https://cfcdn.zulily.com/images/cache/product//194736/zu40629302_main_tm1469564926.jpg");
            item.PictureDetails.PhotoDisplay = PhotoDisplayCodeType.SuperSize;
            item.PictureDetails.PhotoDisplaySpecified = true;

            AddFPItemCall.Item = item;

            //set the item and make the call
            AddFPItemCall.Execute();

            string result = AddFPItemCall.ApiResponse.Ack + " " + AddFPItemCall.ApiResponse.ItemID;
            return result;
        }

        /// <summary>
        /// 02.04.2020
        /// This is the latest code for posting a variation - much of it the listing is taken care of here except for the images.
        /// I get images from the supplier so need to work out how to match them.
        /// Also note, for now this code only handles one variation property (like Size).
        /// </summary>
        /// <param name="storeID"></param>
        /// <param name="sellerListing"></param>
        /// <returns></returns>
        public static string AddFPItemWithVariations_potspans(UserSettingsView settings, int storeID, SellerListing sellerListing)
        {
            //create the context
            ApiContext context = new ApiContext();

            //set the User token
            string token = db.GetToken(settings);
            context.ApiCredential.eBayToken = token;

            //set the server url
            context.SoapApiServerUrl = "https://api.ebay.com/wsapi";

            //enable logging
            context.ApiLogManager = new ApiLogManager();
            context.ApiLogManager.ApiLoggerList.Add(new FileLogger("logebay.txt", true, true, true));
            context.ApiLogManager.EnableLogging = true;

            //set the version
            context.Version = "673";
            context.Version = "949";
            context.Site = SiteCodeType.US;

            //create the call object
            AddFixedPriceItemCall AddFPItemCall = new AddFixedPriceItemCall(context);

            // 02.02.2020 was getting listed with this but i don't know what it is so commenting it out.
            //AddFPItemCall.AutoSetItemUUID = true;

            //create an item object and set the properties
            ItemType item = new ItemType();

            //set the item condition depending on the value from GetCategoryFeatures
            item.ConditionID = 1000; //new with tags

            //Basic properties of a listing
            item.Country = CountryCodeType.US;
            item.Currency = CurrencyCodeType.USD;

            //Track item by SKU
            //item.InventoryTrackingMethod = InventoryTrackingMethodCodeType.SKU;

            //Parent Level SKU
            // item.SKU = "VARPARENT";

            item.Description = "NEW WITH TAGS";
            item.Title = "Rachel Ray Cookware Set Nonstick Enamel Marine Blue Non Stick Enamel Pots Pans";

            // called eBay and they said i was getting charged for subtitle - I never use it so don't use it
            // 02.02/2020 Bonnie credited me back
            // item.SubTitle = "Fast Shipping";

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

            var ssp = new SellerShippingProfileType();
            ssp.ShippingProfileName = "mw";

            var srp = new SellerReturnProfileType();
            srp.ReturnProfileName = "mw";

            sp.SellerPaymentProfile = spp;
            sp.SellerReturnProfile = srp;
            sp.SellerShippingProfile = ssp;

            item.SellerProfiles = sp;

            item.PrimaryCategory = new CategoryType();
            item.PrimaryCategory.CategoryID = sellerListing.PrimaryCategoryID;

            //Add Item Specifics
            NameValueListTypeCollection ItemSpecs = new NameValueListTypeCollection();
            var revisedItemSpecs = Utility.eBayItem.ModifyItemSpecific(sellerListing.ItemSpecifics);
            foreach (var i in revisedItemSpecs)
            {
                var n = Utility.eBayItem.AddItemSpecifics(i);
                ItemSpecs.Add(n);
            }
            item.ItemSpecifics = ItemSpecs;

            //Specify VariationSpecificsSet
            item.Variations = new VariationsType();

            item.Variations.VariationSpecificsSet = new NameValueListTypeCollection();

            string varSpecName = null;
            if (sellerListing.Variations.Count > 0)
            {
                varSpecName = GetVariationSpec(sellerListing.Variations[0]);
            }
            var varNames = GetVariationNames(sellerListing.Variations);
            item.Variations.VariationSpecificsSet.Add(CreateSpecSet(varSpecName, varNames.ToArray()));

            //Specify Variations
            VariationTypeCollection VarCol = new VariationTypeCollection();
            foreach (var varName in varNames)
            {
                var var1 = CreateVariation(varSpecName, varName, 1299);
                VarCol.Add(var1);
            }

            //Add Variation Specific Pictures
            item.Variations.Pictures = new PicturesTypeCollection();

            PicturesType pic = new PicturesType();
            pic.VariationSpecificName = varSpecName;
            pic.VariationSpecificPictureSet = new VariationSpecificPictureSetTypeCollection();

            VariationSpecificPictureSetType VarPicSet1 = new VariationSpecificPictureSetType();
            VarPicSet1.VariationSpecificValue = "Blue";
            StringCollection PicURLVarPicSet1 = new StringCollection();
            PicURLVarPicSet1.Add("https://www.potsandpansplace.com/wp-content/uploads/2014/02/rachael-ray-blue-cookware.jpg");
            VarPicSet1.PictureURL = PicURLVarPicSet1;
            pic.VariationSpecificPictureSet.Add(VarPicSet1);

            VariationSpecificPictureSetType VarPicSet2 = new VariationSpecificPictureSetType();
            VarPicSet2.VariationSpecificValue = "Red";
            StringCollection PicURLVarPicSet2 = new StringCollection();
            PicURLVarPicSet2.Add("https://images.homedepot-static.com/productImages/baa1e8b6-0bae-46a4-b674-eeab2702698c/svn/red-speckle-gibson-home-cookware-sets-985100974m-64_1000.jpg");
            VarPicSet2.PictureURL = PicURLVarPicSet2;
            pic.VariationSpecificPictureSet.Add(VarPicSet2);

            VariationSpecificPictureSetType VarPicSet3 = new VariationSpecificPictureSetType();
            VarPicSet3.VariationSpecificValue = "Gray";
            StringCollection PicURLVarPicSet3 = new StringCollection();
            PicURLVarPicSet3.Add("https://cdnimg.webstaurantstore.com/images/products/xxl/469443/1754390.jpg");
            VarPicSet3.PictureURL = PicURLVarPicSet3;
            pic.VariationSpecificPictureSet.Add(VarPicSet3);

            VariationSpecificPictureSetType VarPicSet4 = new VariationSpecificPictureSetType();
            VarPicSet4.VariationSpecificValue = "Orange";
            StringCollection PicURLVarPicSet4 = new StringCollection();
            PicURLVarPicSet4.Add("https://shop.r10s.jp/alphaespace/cabinet/img18/2006076_10.jpg");
            VarPicSet4.PictureURL = PicURLVarPicSet4;
            pic.VariationSpecificPictureSet.Add(VarPicSet4);

            item.Variations.Pictures.Add(pic);

            item.Variations.Variation = VarCol;

            //Add item level pictures
            item.PictureDetails = new PictureDetailsType();
            item.PictureDetails.PictureURL = new StringCollection();
            item.PictureDetails.PictureURL.Add("https://www.potsandpansplace.com/wp-content/uploads/2014/02/rachael-ray-blue-cookware.jpg");
            item.PictureDetails.PhotoDisplay = PhotoDisplayCodeType.SuperSize;
            item.PictureDetails.PhotoDisplaySpecified = true;

            AddFPItemCall.Item = item;

            //set the item and make the call
            AddFPItemCall.Execute();

            string result = AddFPItemCall.ApiResponse.Ack + " " + AddFPItemCall.ApiResponse.ItemID;
            return result;
        }

        protected static NameValueListType CreateSpecSet(string specName, params string[] attrNames)
        {
            NameValueListType NVListVS1 = new NameValueListType();
            NVListVS1.Name = specName;
            StringCollection VSvaluecollection1 = new StringCollection();
            String[] Size = attrNames;
            VSvaluecollection1.AddRange(Size);

            NVListVS1.Value = VSvaluecollection1;
            return NVListVS1;
        }
        public static string GetVariationSpec(Variation variation)
        {
            var specName = variation.VariationSpecifics.NameValueList.Name;
            return specName;
        }
        public static List<string> GetVariationNames(List<Variation> variations)
        {
            var varNames = new List<string>();
            foreach (var variation in variations)
            {
                var varName = variation.VariationSpecifics.NameValueList.Value;
                varNames.Add(varName);
            }
            return varNames;
        }
        public static VariationType CreateVariation(string varSpecName, string varName, double listPrice)
        {
            var vpd = new VariationProductListingDetailsType();
            vpd.UPC = "Does not apply";

            //Variation 1 - Black S
            VariationType var1 = new VariationType();
            //var1.SKU = "VAR1";
            var1.VariationProductListingDetails = vpd;
            var1.Quantity = 1;
            var1.StartPrice = new AmountType();
            var1.StartPrice.currencyID = CurrencyCodeType.USD;
            var1.StartPrice.Value = listPrice;
            var1.VariationSpecifics = new NameValueListTypeCollection();

            NameValueListType Var1Spec1 = new NameValueListType();
            StringCollection Var1Spec1Valuecoll = new StringCollection();
            Var1Spec1.Name = varSpecName;
            Var1Spec1Valuecoll.Add(varName);
            Var1Spec1.Value = Var1Spec1Valuecoll;
            var1.VariationSpecifics.Add(Var1Spec1);
            return var1;
        }
        /// <summary>
        /// Try to update variation.
        /// 
        /// add/remove variation
        /// https://ebaydts.com/eBayKBDetails?KBid=1896
        /// 
        /// </summary>
        private void ReviseFixedPriceItem_addremove()
        {
            //create the context
            ApiContext context = new ApiContext();

            //set the User token
            context.ApiCredential.eBayToken = "Your token";

            //set the server url
            context.SoapApiServerUrl = "https://api.sandbox.ebay.com/wsapi";

            //enable logging
            context.ApiLogManager = new ApiLogManager();
            context.ApiLogManager.ApiLoggerList.Add(new FileLogger("logebay.txt", true, true, true));
            context.ApiLogManager.EnableLogging = true;

            //set the version
            context.Version = "727";
            context.Site = SiteCodeType.Australia;

            ReviseFixedPriceItemCall reviseFP = new ReviseFixedPriceItemCall(context);

            ItemType item = new ItemType();
            item.SKU = "6618";

            VariationTypeCollection VarCol = new VariationTypeCollection();

            //Add a new Variation - Black M
            VariationType var1 = new VariationType();
            var1.SKU = "1234";
            var1.Quantity = 10;
            var1.StartPrice = new AmountType();
            var1.StartPrice.currencyID = CurrencyCodeType.AUD;
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
            Var1Spec2Valuecoll.Add("M");
            Var1Spec2.Value = Var1Spec2Valuecoll;

            var1.VariationSpecifics.Add(Var1Spec2);

            VarCol.Add(var1);

            //Delete existing Variation Blue L
            VariationType var4 = new VariationType();
            var4.Delete = true;
            //Variation is identified by its SKU
            var4.SKU = "7562";
            VarCol.Add(var4);

            item.Variations = new VariationsType();
            item.Variations.Variation = VarCol;

            reviseFP.Item = item;

            reviseFP.Execute();
            Console.WriteLine(reviseFP.ApiResponse.Ack + " Revised SKU " + reviseFP.SKU);
        }

        /// <summary>
        /// 
        /// update item specifics
        /// https://ebaydts.com/eBayKBDetails?KBid=1647
        /// 
        /// </summary>
        private void ReviseFixedPriceItem_itemspecific()
        {

            //create the context
            ApiContext context = new ApiContext();

            //set the User token
            context.ApiCredential.eBayToken = "Your token";

            //set the server url
            context.SoapApiServerUrl = "https://api.sandbox.ebay.com/wsapi";

            //enable logging
            context.ApiLogManager = new ApiLogManager();
            context.ApiLogManager.ApiLoggerList.Add(new FileLogger("logebay.txt", true, true, true));
            context.ApiLogManager.EnableLogging = true;

            //set the version
            context.Version = "817";
            context.Site = SiteCodeType.UK;

            ReviseFixedPriceItemCall reviseFP = new ReviseFixedPriceItemCall(context);

            ItemType item = new ItemType();
            item.SKU = "5591";

            //Specify the entire item specifics container
            item.ItemSpecifics = new NameValueListTypeCollection();

            NameValueListTypeCollection ItemSpecs = new NameValueListTypeCollection();

            NameValueListType nv1 = new NameValueListType();
            StringCollection valueCol1 = new StringCollection();

            nv1.Name = "Brand";
            valueCol1.Add("Ralph Lauren");
            nv1.Value = valueCol1;

            ItemSpecs.Add(nv1);

            NameValueListType nv2 = new NameValueListType();
            StringCollection valueCol2 = new StringCollection();
            nv2.Name = "Size";
            valueCol2.Add("M");
            nv2.Value = valueCol2;
            ItemSpecs.Add(nv2);

            NameValueListType nv3 = new NameValueListType();
            StringCollection valueCol3 = new StringCollection();
            nv3.Name = "Colour";
            valueCol3.Add("Blue");
            nv3.Value = valueCol3;
            ItemSpecs.Add(nv3);

            item.ItemSpecifics = ItemSpecs;

            reviseFP.Item = item;
            reviseFP.Execute();
            Console.WriteLine(reviseFP.ApiResponse.Ack + " Revised SKU " + reviseFP.SKU);
        }

        public static void ReviseFixedPriceItem(UserSettingsView settings, string listingItemID, string varSpecName, string varName)
        {
            //create the context
            ApiContext context = new ApiContext();

            //set the User token
            var token = db.GetToken(settings);
            context.ApiCredential.eBayToken = token;

            //set the server url
            context.SoapApiServerUrl = "https://api.ebay.com/wsapi";

            //enable logging
            context.ApiLogManager = new ApiLogManager();
            context.ApiLogManager.ApiLoggerList.Add(new FileLogger("logebay.txt", true, true, true));
            context.ApiLogManager.EnableLogging = true;

            //set the version
            context.Version = "817";
            context.Site = SiteCodeType.US;

            ReviseFixedPriceItemCall reviseFP = new ReviseFixedPriceItemCall(context);

            ItemType item = new ItemType();
            item.ItemID = listingItemID;

            /*
            //Specify the entire item specifics container
            item.ItemSpecifics = new NameValueListTypeCollection();

            NameValueListTypeCollection ItemSpecs = new NameValueListTypeCollection();

            NameValueListType nv1 = new NameValueListType();
            StringCollection valueCol1 = new StringCollection();

            nv1.Name = "Brand";
            valueCol1.Add("Ralph Lauren");
            nv1.Value = valueCol1;

            ItemSpecs.Add(nv1);

            NameValueListType nv2 = new NameValueListType();
            StringCollection valueCol2 = new StringCollection();
            nv2.Name = "Size";
            valueCol2.Add("M");
            nv2.Value = valueCol2;
            ItemSpecs.Add(nv2);

            NameValueListType nv3 = new NameValueListType();
            StringCollection valueCol3 = new StringCollection();
            nv3.Name = "Colour";
            valueCol3.Add("Blue");
            nv3.Value = valueCol3;
            ItemSpecs.Add(nv3);

            item.ItemSpecifics = ItemSpecs;
            */

            // try to just update qty

            VariationTypeCollection VarCol = new VariationTypeCollection();
            VariationType var1 = new VariationType();
            var1.Quantity = 0;

            NameValueListType Var1Spec1 = new NameValueListType();
            StringCollection Var1Spec1Valuecoll = new StringCollection();
            Var1Spec1.Name = varSpecName;
            Var1Spec1Valuecoll.Add(varName);
            Var1Spec1.Value = Var1Spec1Valuecoll;

            var1.VariationSpecifics = new NameValueListTypeCollection();
            var1.VariationSpecifics.Add(Var1Spec1);

            VarCol.Add(var1);
            item.Variations = new VariationsType();
            item.Variations.Variation = VarCol;

            reviseFP.Item = item;
            reviseFP.Execute();
            Console.WriteLine(reviseFP.ApiResponse.Ack + " Revised itemID " + reviseFP.ItemID);
        }

    }
}