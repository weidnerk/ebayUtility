/*
 * eBayServiceCall
 * 
 * 
 * 
 */
using dsmodels;
using eBay.Service.Core.Soap;

namespace Utility
{
    public class EbayCalls
    {

        public static eBayAPIInterfaceService eBayServiceCall(IUserSettingsView settings, string CallName, string siteID)
        {
            string endpoint = "https://api.ebay.com/wsapi";
            //string siteId = "0";
            string appId = settings.AppID;
            string devId = settings.DevID;
            string certId = settings.CertID;
            string version = "965";
            // Build the request URL
            string requestURL = endpoint
            + "?callname=" + CallName
            + "&siteid=" + siteID
            + "&appid=" + appId
            + "&version=" + version
            + "&routing=default";

            eBayAPIInterfaceService service = new eBayAPIInterfaceService();
            // Assign the request URL to the service locator.
            service.Url = requestURL;
            // Set credentials
            service.RequesterCredentials = new CustomSecurityHeaderType();
            service.RequesterCredentials.eBayAuthToken = settings.Token;
            service.RequesterCredentials.Credentials = new UserIdPasswordType();
            service.RequesterCredentials.Credentials.AppId = appId;
            service.RequesterCredentials.Credentials.DevId = devId;
            service.RequesterCredentials.Credentials.AuthCert = certId;
            return service;
        }
    }
}