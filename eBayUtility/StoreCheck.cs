﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using dsmodels;
using eBay.Service.Core.Soap;
using eBayUtility;

namespace Utility
{
    public static class StoreCheck
    {
        private static IRepository _repository;

        public static void Init(IRepository repository)
        {
            _repository = repository;
        }
        /// <summary>
        /// does listing id exist in the db?
        /// </summary>
        /// <param name="itemid"></param>
        /// <returns></returns>
        public static Listing LookupItemid(UserSettingsView settings, string itemid)
        {
            var result = _repository.Context.Listings.Where(x => x.ListedItemID == itemid && x.StoreID == settings.StoreID).SingleOrDefault();
            if (result != null)
            {
                return result;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// items are listed that are not in the db
        /// </summary>
        /// <param name="totalListed"></param>
        /// <returns></returns>
        public static List<string> DBIsMissingItems_notused(UserSettingsView settings, ref ItemTypeCollection storeItems)
        {
            var items = new List<string>();
            /*
            int cnt = 0;
            if (storeItems.Count == 0)
            {
                storeItems = ebayAPIs.GetSellerList(settings, out string errMsg);
            }
            if (storeItems != null)
            {
                // scan each item in store - is it in db?
                foreach (ItemType oItem in storeItems)
                {
                    if (oItem.Quantity > 0)
                    {
                        int stop = 99;
                    }
                    bool r = Utility.StoreCheck.LookupItemid(settings, oItem.ItemID);
                    if (!r)
                    {
                        items.Add(oItem.Title);
                        ++cnt;
                    }
                }
            }
            */
            return items;
        }
        public static IStoreAnalysis Analysis(UserSettingsView settings, ref ItemTypeCollection storeItems)
        {
            IStoreAnalysis analysis = new StoreAnalysis();
            var items = new List<string>();
            var qtyMismatch = new List<string>();
            int cnt = 0;
            int qtyMismatchCnt = 0;
            if (storeItems.Count == 0)
            {
                storeItems = ebayAPIs.GetSellerList(settings, out string errMsg);
            }
            if (storeItems != null)
            {
                // scan each item in store - is it in db?
                foreach (ItemType oItem in storeItems)
                {
                    //if (oItem.ItemID == "224079107216")
                    //{
                    //    int stop = 99;
                    //}
                    var listing = Utility.StoreCheck.LookupItemid(settings, oItem.ItemID);
                    if (listing == null)
                    {
                        items.Add(oItem.Title);
                        ++cnt;
                    }
                    else
                    {
                        if (listing.Qty != (oItem.Quantity - oItem.SellingStatus.QuantitySold))
                        {
                            qtyMismatch.Add(oItem.Title);
                            ++qtyMismatchCnt;
                        }
                    }
                }
                analysis.DBIsMissingItems = items;
                analysis.QtyMismatch = qtyMismatch;
                analysis.InActive = GetInactive(settings.StoreID);
            }
            return analysis;
        }
        private static int GetInactive(int storeID)
        {
            var ret = _repository.GetListings(storeID, false, true).Where(p => p.InActive).Count();
            return ret;
        }
    }
}