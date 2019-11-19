/*
 * 
 * had to get shopping wsdl from here: https://github.com/tonicsoft/ebay-shopping-api/blob/master/src/main/resources/com/ebay/ShoppingService.wsdl
 * (click 'Raw')
 * 
 */
using eBayUtility.WebReference;
using eBayUtility.WebReference1;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Web;

namespace Utility
{
    public class CustomShoppingService : Shopping
    {
        public string appID { get; set; }
        protected override WebRequest GetWebRequest(Uri uri)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)base.GetWebRequest(uri);
                request.Headers.Add("X-EBAY-SOA-SECURITY-APPNAME", this.appID);
                request.Headers.Add("X-EBAY-SOA-OPERATION-NAME", "GetSingleItem");
                request.Headers.Add("X-EBAY-SOA-SERVICE-NAME", "ShoppingService");
                request.Headers.Add("X-EBAY-SOA-MESSAGE-PROTOCOL", "SOAP11");
                request.Headers.Add("X-EBAY-SOA-SERVICE-VERSION", "1.0.0");
                request.Headers.Add("X-EBAY-SOA-GLOBAL-ID", "EBAY-US");
                return request;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }

    public class CustomFindingService : FindingService
    {
        public string appID { get; set; }
        protected override WebRequest GetWebRequest(Uri uri)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)base.GetWebRequest(uri);
                request.Headers.Add("X-EBAY-SOA-SECURITY-APPNAME", this.appID);
                request.Headers.Add("X-EBAY-SOA-OPERATION-NAME", "findItemsByKeywords");
                request.Headers.Add("X-EBAY-SOA-SERVICE-NAME", "FindingService");
                request.Headers.Add("X-EBAY-SOA-MESSAGE-PROTOCOL", "SOAP11");
                request.Headers.Add("X-EBAY-SOA-SERVICE-VERSION", "1.0.0");
                request.Headers.Add("X-EBAY-SOA-GLOBAL-ID", "EBAY-US");
                return request;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
    public class CustomFindAdvanced : FindingService
    {
        public string appID { get; set; }
        protected override WebRequest GetWebRequest(Uri uri)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)base.GetWebRequest(uri);
                request.Headers.Add("X-EBAY-SOA-SECURITY-APPNAME", this.appID);
                request.Headers.Add("X-EBAY-SOA-OPERATION-NAME", "findItemsAdvanced");
                request.Headers.Add("X-EBAY-SOA-SERVICE-NAME", "FindingService");
                request.Headers.Add("X-EBAY-SOA-MESSAGE-PROTOCOL", "SOAP11");
                request.Headers.Add("X-EBAY-SOA-SERVICE-VERSION", "1.0.0");
                request.Headers.Add("X-EBAY-SOA-GLOBAL-ID", "EBAY-US");
                return request;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }

    public class CustomFindSold : FindingService
    {
        public string appID { get; set; }
        protected override WebRequest GetWebRequest(Uri uri)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)base.GetWebRequest(uri);
                request.Headers.Add("X-EBAY-SOA-SECURITY-APPNAME", this.appID);
                request.Headers.Add("X-EBAY-SOA-OPERATION-NAME", "findCompletedItems");
                request.Headers.Add("X-EBAY-SOA-SERVICE-NAME", "FindingService");
                request.Headers.Add("X-EBAY-SOA-MESSAGE-PROTOCOL", "SOAP11");
                request.Headers.Add("X-EBAY-SOA-SERVICE-VERSION", "1.0.0");
                request.Headers.Add("X-EBAY-SOA-GLOBAL-ID", "EBAY-US");
                return request;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }

    public class CustomFindItemByProductId : FindingService
    {
        public string appID { get; set; }
        protected override WebRequest GetWebRequest(Uri uri)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)base.GetWebRequest(uri);
                request.Headers.Add("X-EBAY-SOA-SECURITY-APPNAME", this.appID);
                request.Headers.Add("X-EBAY-SOA-OPERATION-NAME", "findItemsByProduct");
                request.Headers.Add("X-EBAY-SOA-SERVICE-NAME", "FindingService");
                request.Headers.Add("X-EBAY-SOA-MESSAGE-PROTOCOL", "SOAP11");
                request.Headers.Add("X-EBAY-SOA-SERVICE-VERSION", "1.0.0");
                request.Headers.Add("X-EBAY-SOA-GLOBAL-ID", "EBAY-US");
                return request;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}