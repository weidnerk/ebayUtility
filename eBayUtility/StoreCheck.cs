using System;
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
        static DataModelsDB db = new DataModelsDB();
        /// <summary>
        /// does listing id exist in the db?
        /// </summary>
        /// <param name="itemid"></param>
        /// <returns></returns>
        public static bool LookupItemid(UserSettingsView settings, string itemid)
        {
            var result = db.Listings.Where(x => x.ListedItemID == itemid && x.StoreID == settings.StoreID).ToList();
            if (result.Count == 0) return false;
            return true;
        }

        /// <summary>
        /// items are listed that are not in the db
        /// </summary>
        /// <param name="totalListed"></param>
        /// <returns></returns>
        public static List<string> DBIsMissingItems(UserSettingsView settings, ref ItemTypeCollection storeItems)
        {
            var items = new List<string>();
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
            return items;
        }
    }
}