/**
 * Namespace: Willmav5000.iHacktivate
 * Class: ActivationServer
 * Description: A local activation server for iOS devices
 * Author: Will Mather
 * Twitter: willmav5000
 * 
 * Copyright (c) 2020 Will Mather.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Text.RegularExpressions;

namespace Willmav5000.iHacktivate
{
    class ActivationServer
    {
        public void StartServer()
        {
            HttpListener actServer = new HttpListener();
            actServer.Prefixes.Add("http://localhost:4228/");
            actServer.Start();

            while (true)
            {
                HttpListenerContext context = actServer.GetContext();
                HttpListenerResponse response = context.Response;
                HttpListenerRequest request = context.Request;

                string data = GetRequestPostData(request);

                if (string.IsNullOrEmpty(data))
                {
                    byte[] noBuff = Encoding.UTF8.GetBytes("<h1>Nothing to see here!</h1>");

                    response.ContentLength64 = noBuff.Length;
                    response.Headers.Add("Content-type", "text/html; charset=UTF-8");
                    response.Headers.Add("Server", "iHacktivate\r\n\r\n");
                    Stream ns = response.OutputStream;
                    ns.Write(noBuff, 0, noBuff.Length);
                    context.Response.Close();
                    continue;
                }
                    
                Dictionary<string, string> activationData = GetActivationData(data);

                string fpkd = FairPlayKeyData("");
                string atc = AccountTokenCertificate();
                string dc = DeviceCertificate();
                string at = AccountToken(activationData);
                string ats = SignData(at);
                at = Convert.ToBase64String(Encoding.UTF8.GetBytes(at));

                string msg = ActivationData(fpkd, atc, dc, ats, at);

                string subDir = @"Data//" + activationData["serialNumber"] + "//";
                bool exists = Directory.Exists(subDir);

                if (!exists)
                    Directory.CreateDirectory(subDir);

                File.WriteAllText(subDir + "activation_record.plist", msg);

                byte[] buffer = Encoding.UTF8.GetBytes(msg);

                response.ContentLength64 = buffer.Length;
                response.Headers.Add("Content-type", "application/xml");
                response.Headers.Add("Server", "iHacktivate\r\n\r\n");
                Stream st = response.OutputStream;
                st.Write(buffer, 0, buffer.Length);

                context.Response.Close();

                if (!string.IsNullOrEmpty(msg))
                {
                    actServer.Stop();
                    break;
                }
            }
        }

        public static string GetRequestPostData(HttpListenerRequest request)
        {
            if (!request.HasEntityBody || !request.ContentType.ToLowerInvariant().StartsWith("multipart/form-data"))
            {
                return null;
            }
            using (Stream body = request.InputStream)
            {
                using (StreamReader reader = new StreamReader(body, request.ContentEncoding))
                {
                    var contentTypeRegex = new Regex("^multipart\\/form-data;\\s*boundary=(.*)$", RegexOptions.IgnoreCase);
                    var boundaryRegex = new Regex("boundary=(.*)$", RegexOptions.IgnoreCase);
                    var bodyStream = reader.ReadToEnd();

                    if (contentTypeRegex.IsMatch(request.ContentType))
                    {
                        var boundary = boundaryRegex.Match(request.ContentType).Groups[1].Value;

                        bodyStream = bodyStream.Replace("Content-Disposition: form-data; name=\"activation-info\"", "");
                        bodyStream = bodyStream.Replace(boundary, "");
                        bodyStream = bodyStream.Replace("--", "");
                        bodyStream = bodyStream.Replace("\r\n", "");
                        bodyStream = bodyStream.Replace("<data>\n", "<data>");
                        bodyStream = bodyStream.Replace("</data>\n", "</data>");
                        bodyStream = bodyStream.Replace("\t", "");

                        return bodyStream;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        private Dictionary<string, string> GetActivationData(string data)
        {
            Dictionary<string, string> activationData = new Dictionary<string, string>();
            string xmlData = "";

            XmlDocument xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.LoadXml(data);
                foreach (XmlNode xmlNode in xmlDoc.DocumentElement.ChildNodes[1])
                {
                    xmlData = xmlNode.InnerText;
                    xmlData = Encoding.Default.GetString(Convert.FromBase64String(xmlData));
                }
            }
            catch (FileNotFoundException)
            {
                Debug.WriteLine("Wrong!");
            }

            xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.LoadXml(xmlData);
                XmlNodeList dict = xmlDoc.GetElementsByTagName("dict");

                foreach (XmlNode xmlNode in dict)
                {
                    var nodes = xmlNode.ChildNodes;
                    for (int i = 0; i < nodes.Count - 1; i = i + 2)
                    {
                        switch (nodes.Item(i).InnerText)
                        {
                            case "ActivationRandomness":
                                activationData.Add("activationRandomness", nodes.Item(i + 1).InnerText);
                                break;
                            case "ActivationState":
                                activationData.Add("activationState", nodes.Item(i + 1).InnerText);
                                break;
                            case "BasebandSerialNumber":
                                activationData.Add("basebandSerialNumber", nodes.Item(i + 1).InnerText);
                                break;
                            case "DeviceCertRequest":
                                activationData.Add("deviceCertRequest", Encoding.Default.GetString(Convert.FromBase64String(nodes.Item(i + 1).InnerText)));
                                break;
                            case "DeviceClass":
                                activationData.Add("deviceClass", nodes.Item(i + 1).InnerText);
                                break;
                            case "IntegratedCircuitCardIdentity":
                                activationData.Add("integratedCircuitCardIdentity", nodes.Item(i + 1).InnerText);
                                break;
                            case "InternationalMobileEquipmentIdentity":
                                activationData.Add("internationalMobileEquipmentIdentity", nodes.Item(i + 1).InnerText);
                                break;
                            case "InternationalMobileSubscriberIdentity":
                                activationData.Add("internationalMobileSubscriberIdentity", nodes.Item(i + 1).InnerText);
                                break;
                            case "MobileEquipmentIdentifier":
                                activationData.Add("mobileEquipmentIdentifier", nodes.Item(i + 1).InnerText);
                                break;
                            case "ProductType":
                                activationData.Add("productType", nodes.Item(i + 1).InnerText);
                                break;
                            case "ProductVersion":
                                activationData.Add("productVersion", nodes.Item(i + 1).InnerText);
                                break;
                            case "SerialNumber":
                                activationData.Add("serialNumber", nodes.Item(i + 1).InnerText);
                                break;
                            case "UniqueChipID":
                                activationData.Add("uniqueChipID", nodes.Item(i + 1).InnerText);
                                break;
                            case "UniqueDeviceID":
                                activationData.Add("uniqueDeviceID", nodes.Item(i + 1).InnerText);
                                break;
                            case "WildcardTicket":
                                activationData.Add("wildcardTicket", nodes.Item(i + 1).InnerText);
                                break;
                        }
                    }
                }
            }

            catch (FileNotFoundException)
            {
                Debug.WriteLine("Wrong!");
            }

            return activationData;
        }

        private string FairPlayKeyData(string device)
        {
            string fpkd = "LS0tLS1CRUdJTiBDT05UQUlORVItLS0tLQpBQUVBQVNyTkhHMW5jc1N5NU5HVGpNcUpYZndTcnRSeklqZklQa1E4NnY4TldJTTZ3L1J6L1RhblFEKzNtOERwCnFkNmhVZy9BUHVOK2orYmJybGlDSEt5SWtMYTZKL0ZtNFVaYlZZZjhVQ2dGWFltMDJEZ1JqQ1FGUHBoUjJ6eVMKd2V4a0dKaVFNNE1pQUdPY2ZvZjBETG5ldXpUODlqRnM3eExUWjEzbXRFT2txd3BVMU81elp0UGtYK2VSUUJjdgp0RjgrYXNDZGQrVDlYQ29OSXhadGE4NnlVeFRLSWdGMk5nL3R4VnNyUkcwV29MdmcxL2d4amlhaElMODBFTjJwCjRJUnlvTUJjV3AyK00rd2FiY3dVZWNEVmVyK1dNSURIeDlkRGdGaWxLQm5Vd1RJVUZoaktwSGQwTHRXRHpzVWUKM1FyKzVvRnkwN21jM1FUc1BFdkx1dzhBbThWMHNCSTVUVXVxaTEycllmQVJmVk9vZ1l4M2FNN0lsMEphZzV2WAo5dWJEUDRrVEVYc1QrQXNjU1UwOHpRVVFDeXpyMUlGZFBJOTBqbFkvMVB0ekJLZHpaL1JZMUtSY2NLQ1VJZW8zCmNsdnRxUVNKWnNhelJmRmg1cmdkNnQ4NWZXeTIzMXZ5bGsyWVlmYjdIeTVhdzBBSzk3OHFKQWg0WDAyZUsyWG8KeXAreVZlVlE2akhGdnZ6am9oUWtMaGQrT2QzZlQ4TVZlNFJiakdTSC9GejZTWTNVSHJ3RTJWcmRGVms3QTJ5awpBdHhhU1dFNHBFQWVlQWRoVEVaTWdaM3ZLRFJDVTBOay85OFlXSThZMlJwVDd2QXRubVZXVC92TU1ZVDdFekxFCnVzNHJiMWczNGdSQnZ1dXR0K3R2ZlZZakcxRHc5WXNwWW9BY1BhZ2Zucmt4V0h2eTAvT1ZaNjdtbkE0UG9hWmgKeEFjQzF0M3BmdElMazFmM3ZTSUZTTHNqSGlYTGd6aTRXL09VNUNsaWJ4amd6Zkt6QWdwNEtZZkRzSHRydHMvagpVOU5zWmhpaFNCSFJPS3RJSVZKZWRJdnBwdW1SSmMrTUFlK09iajNWMFJjUXAyVXQzM2JwNnhBMlZ2VUhEWm9TClM4cUhvZTZmUW85MW1YVEYwSk1NUUZVWlBOYnJLOGlFblNwaDhta3dYVWtzTXJxSHEvSnBpL2RmbmE2WTBXL1oKR1lLelR3WmNJSUpNNWxJRDY5UnZCUURTRmhqVVBPTHJkYUNxdmdhUEVYSEZHakg1MWJ0ZWFCbC9YSGRId1gwYwp0K3NPUmdiT3hrRSs1WFYybjE2aWt1TkpMK0xLMUNDUGgrYVpUUmFmNzFQSWwrVXpaNzgvdUF3UUFTR1gzT25pCnUrU045bnNpNGhLUDMrT3BwcVJWM1NtekxEalhqV0czb3dlNXJNSXgzM3ZJQmhzNDYvdHE2a05WSlNHWWFNUTQKTEllS1lLS2dJbThqRldkVFM1WFRQdFYwZjl4R0V6aDNPT05XRjQ1eS93L1d2V0VwaGJJWCtra1QrT0Z5bDFRdgpYbnZuYjkrT2FTUTJSa3VGTGdjczl1MHBENXdxSnZDVHFia0hOeXJHTGdNSXlBcWszeWsxOHgxcGVYbHE3bjY2CkdubkNPRUJUbE5WNlZCMnlvRzBOQWJXMUFiZzhnYUw4TXVhV1NHeU05b3cvVW9KSkoyRTFIR0ptNGF5UEc4aUYKZ1NxZndQLy9KY09SVks2WEgwNFlnaTJ3SzAvQjR3emdtVDM0RHh5bFpwYlBoLzY2R3BxOFVOWVBJWk5RUWVrWgpXUWlOZXhFMytjSXUwblVjU2xBU2hTdmx6bXJ3enRUZndaL1lHUEZkbkhhaG5yUjRkNEI2Mlg0bWR0Tm1lQWg4ClF4Uk5sdVNqN2p4UzZoMWpBelVnRTlFUXFaTGJ3NHpLMWRGaVhqaml5MEJLV29jTVNxRHBaa2R5ZForUE1zakgKSE92SEZnOS9ZMXVSOFVqMjdmNmlWZUZlcC9OU0N5M1FRZVFRbkdxMWovdTJhVHVuCi0tLS0tRU5EIENPTlRBSU5FUi0tLS0tCg==";
            return fpkd;

        }

        private string AccountTokenCertificate()
        {
            string atc = "LS0tLS1CRUdJTiBDRVJUSUZJQ0FURS0tLS0tCk1JSURaekNDQWsrZ0F3SUJBZ0lCQWpBTkJna3Foa2lHOXcwQkFRVUZBREI1TVFzd0NRWURWUVFHRXdKVlV6RVQKTUJFR0ExVUVDaE1LUVhCd2JHVWdTVzVqTGpFbU1DUUdBMVVFQ3hNZFFYQndiR1VnUTJWeWRHbG1hV05oZEdsdgpiaUJCZFhSb2IzSnBkSGt4TFRBckJnTlZCQU1USkVGd2NHeGxJR2xRYUc5dVpTQkRaWEowYVdacFkyRjBhVzl1CklFRjFkR2h2Y21sMGVUQWVGdzB3TnpBME1UWXlNalUxTURKYUZ3MHhOREEwTVRZeU1qVTFNREphTUZzeEN6QUoKQmdOVkJBWVRBbFZUTVJNd0VRWURWUVFLRXdwQmNIQnNaU0JKYm1NdU1SVXdFd1lEVlFRTEV3eEJjSEJzWlNCcApVR2h2Ym1VeElEQWVCZ05WQkFNVEYwRndjR3hsSUdsUWFHOXVaU0JCWTNScGRtRjBhVzl1TUlHZk1BMEdDU3FHClNJYjNEUUVCQVFVQUE0R05BRENCaVFLQmdRREZBWHpSSW1Bcm1vaUhmYlMyb1BjcUFmYkV2MGQxams3R2JuWDcKKzRZVWx5SWZwcnpCVmRsbXoySkhZdjErMDRJekp0TDdjTDk3VUk3ZmswaTBPTVkwYWw4YStKUFFhNFVnNjExVApicUV0K25qQW1Ba2dlM0hYV0RCZEFYRDlNaGtDN1QvOW83N3pPUTFvbGk0Y1VkemxuWVdmem1XMFBkdU94dXZlCkFlWVk0d0lEQVFBQm80R2JNSUdZTUE0R0ExVWREd0VCL3dRRUF3SUhnREFNQmdOVkhSTUJBZjhFQWpBQU1CMEcKQTFVZERnUVdCQlNob05MK3Q3UnovcHNVYXEvTlBYTlBIKy9XbERBZkJnTlZIU01FR0RBV2dCVG5OQ291SXQ0NQpZR3UwbE01M2cyRXZNYUI4TlRBNEJnTlZIUjhFTVRBdk1DMmdLNkFwaGlkb2RIUndPaTh2ZDNkM0xtRndjR3hsCkxtTnZiUzloY0hCc1pXTmhMMmx3YUc5dVpTNWpjbXd3RFFZSktvWklodmNOQVFFRkJRQURnZ0VCQUY5cW1yVU4KZEErRlJPWUdQN3BXY1lUQUsrcEx5T2Y5ek9hRTdhZVZJODg1VjhZL0JLSGhsd0FvK3pFa2lPVTNGYkVQQ1M5Vgp0UzE4WkJjd0QvK2Q1WlFUTUZrbmhjVUp3ZFBxcWpubTlMcVRmSC94NHB3OE9OSFJEenhIZHA5NmdPVjNBNCs4CmFia29BU2ZjWXF2SVJ5cFhuYnVyM2JSUmhUekFzNFZJTFM2alR5Rll5bVplU2V3dEJ1Ym1taWdvMWtDUWlaR2MKNzZjNWZlREF5SGIyYnpFcXR2eDNXcHJsanRTNDZRVDVDUjZZZWxpblpuaW8zMmpBelJZVHh0UzZyM0pzdlpEaQpKMDcrRUhjbWZHZHB4d2dPKzdidFcxcEZhcjBaakY5L2pZS0tuT1lOeXZDcndzemhhZmJTWXd6QUc1RUpvWEZCCjRkK3BpV0hVRGNQeHRjYz0KLS0tLS1FTkQgQ0VSVElGSUNBVEUtLS0tLQo=";
            return atc;
        }

        private string AccountToken(Dictionary<string, string> activationData)
        {
            string at = "{" +
                        (activationData.ContainsKey("internationalMobileEquipmentIdentity") ? "\"InternationalMobileEquipmentIdentity\" = \"" + activationData["internationalMobileEquipmentIdentity"] + "\";" : "") +
                        (activationData.ContainsKey("mobileEquipmentIdentifier") ? "\"MobileEquipmentIdentifier\" = \"" + activationData["mobileEquipmentIdentifier"] + "\";" : "") +
                        "\"SerialNumber\" = \"" + activationData["serialNumber"] + "\";" +
                        (activationData.ContainsKey("internationalMobileSubscriberIdentity") ? "\"InternationalMobileSubscriberIdentity\" = \"" + activationData["internationalMobileSubscriberIdentity"] + "\";" : "") +
                        "\"ProductType\" = \"" + activationData["productType"] + "\";" +
                        "\"UniqueDeviceID\" = \"" + activationData["uniqueDeviceID"] + "\";" +
                        "\"ActivationRandomness\" = \"" + activationData["activationRandomness"] + "\";" +
                        "\"ActivityURL\" = \"https://albert.apple.com/deviceservices/activity\";" +
                        "\"IntegratedCircuitCardIdentity\" = \"" + (activationData.ContainsKey("integratedCircuitCardIdentity") ? activationData["integratedCircuitCardIdentity"] : "") + "\";" +
                        (activationData["deviceClass"] == "iPhone" ? "\"CertificateURL\" = \"https://albert.apple.com/deviceservices/certifyMe\";" : "") +
                        (activationData["deviceClass"] == "iPhone" ? "\"PhoneNumberNotificationURL\" = \"https://albert.apple.com/deviceservices/phoneHome\";" : "") +
                        "\"WildcardTicket\" = \"" + (activationData.ContainsKey("wildcardTicket") ? activationData["wildcardTicket"] : "") + "\";" +
                        "}";
            return at;
        }

        private string DeviceCertificate()
        {
            string dc = "LS0tLS1CRUdJTiBDRVJUSUZJQ0FURS0tLS0tCk1JSUM4ekNDQWx5Z0F3SUJBZ0lLQkFWclNHVHROTDZ6d3pBTkJna3Foa2lHOXcwQkFRVUZBREJhTVFzd0NRWUQKVlFRR0V3SlZVekVUTUJFR0ExVUVDaE1LUVhCd2JHVWdTVzVqTGpFVk1CTUdBMVVFQ3hNTVFYQndiR1VnYVZCbwpiMjVsTVI4d0hRWURWUVFERXhaQmNIQnNaU0JwVUdodmJtVWdSR1YyYVdObElFTkJNQjRYRFRFNU1USXlOekUyCk5ETXlPVm9YRFRJeU1USXlOekUyTkRNeU9Wb3dnWU14TFRBckJnTlZCQU1XSkRjMVJURTVNRVJGTFVZNFJVTXQKTkVJMFFpMUNRVVpHTFRNM00wWTVNakZETURsQk1ERUxNQWtHQTFVRUJoTUNWVk14Q3pBSkJnTlZCQWdUQWtOQgpNUkl3RUFZRFZRUUhFd2xEZFhCbGNuUnBibTh4RXpBUkJnTlZCQW9UQ2tGd2NHeGxJRWx1WXk0eER6QU5CZ05WCkJBc1RCbWxRYUc5dVpUQ0JuekFOQmdrcWhraUc5dzBCQVFFRkFBT0JqUUF3Z1lrQ2dZRUF3ZmFwSUZDSjBGeTQKNWZRUFBhK1ljWTE0d0JRcHI2ME13ZXhON2pyeUJMVlUrVS9mSUhCV2VPajBXUmEzRi9yc1FaU2dzWldkOXBmMApUbklpMG5xVVZmUWtSOEpQZS84T2RNOE9ZTW1xK0lGYW5CSkJtcUNyaDBUUDUwalhiMVhwSXdXVzhjc0luK2c1ClFJQ2pCOEFqandUcFBFOURqOTVGSmt3akd3ZUF4eFVDQXdFQUFhT0JsVENCa2pBZkJnTlZIU01FR0RBV2dCU3kKL2lFalJJYVZhbm5WZ1NhT2N4RFlwMHlPZERBZEJnTlZIUTRFRmdRVVVuZXY3Y3paVlA2QkJxLzhQTGpMcWl5YwpmV293REFZRFZSMFRBUUgvQkFJd0FEQU9CZ05WSFE4QkFmOEVCQU1DQmFBd0lBWURWUjBsQVFIL0JCWXdGQVlJCkt3WUJCUVVIQXdFR0NDc0dBUVVGQndNQ01CQUdDaXFHU0liM1kyUUdDZ0lFQWdVQU1BMEdDU3FHU0liM0RRRUIKQlFVQUE0R0JBRThXU3dCSVF2aTYweGY2UFdidjF6c3g0Wm01VEltQzZuSzF4cXcvU1hhKzZvTU9RQnhId0hqQwo5Y1M1WTBYSG1zQW1PYVhvVGRyMzZ1SXgwTE8yLzkyWVhBZ1JUZWVMNURoVU55ZHg3Q3ZBWmVlK0FmRnFxc0FxCld3V3IxK3hxckIrb2JEaG9ldk5oWXB6Q1lrZjV6VGpQcUZrTHRMQWZnc1hZT1kzSVRsZlgKLS0tLS1FTkQgQ0VSVElGSUNBVEUtLS0tLQo=";
            return dc;
        }

        private string ActivationData(string fpkd, string atc, string dc, string ats, string at)
        {
            string ad =
                "<plist version=\"1.0\">\n" +
                "    <dict>\n" +
                "        <key>iphone-activation</key>\n" +
                "        <dict>\n" +
                "            <key>activation-record</key>\n" +
                "            <dict>\n" +
                "                <key>FairPlayKeyData</key>\n" +
                "                <data>[FairPlayKeyData]</data>\n" +
                "                <key>AccountTokenCertificate</key>\n" +
                "                <data>[AccountTokenCertificate]</data>\n" +
                "                <key>DeviceCertificate</key>\n" +
                "                <data>[DeviceCertificate]</data>\n" +
                "                <key>AccountTokenSignature</key>\n" +
                "                <data>[AccountTokenSignature]</data>\n" +
                "                <key>AccountToken</key>\n" +
                "                <data>[AccountToken]</data>\n" +
                "            </dict>\n" +
                "            <key>unbrick</key>\n" +
                "            <true/>\n" +
                "            <key>show-settings</key>\n" +
                "            <true/>\n" +
                "        </dict>\n" +
                "    </dict>\n" +
                "</plist>";

            ad = ad
                .Replace("[FairPlayKeyData]", fpkd)
                .Replace("[AccountTokenCertificate]", atc)
                .Replace("[DeviceCertificate]", dc)
                .Replace("[AccountTokenSignature]", ats)
                .Replace("[AccountToken]", at);

            return ad;
        }

        public static string SignData(string message)
        {
            X509Certificate2 certs = new X509Certificate2(@"Data//certs.pfx");
            RSA rsaPriv = (RSA)certs.PrivateKey;

            var encoder = new UTF8Encoding();
            byte[] signedBytes;
            byte[] originalData = encoder.GetBytes(message);

            try
            {
                signedBytes = rsaPriv.SignData(originalData, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
            }
            catch (CryptographicException e)
            {
                Debug.WriteLine(e.Message);
                return null;
            }

            return Convert.ToBase64String(signedBytes);
        }
    }
}
