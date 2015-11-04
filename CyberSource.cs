using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using CyberSourceClient = CommerceGeneration.MerchantServicesPipeline.CyberSourceServiceClient;
using CommerceGeneration.MerchantServicesPipeline.Interfaces;

namespace CommerceGeneration.MerchantServicesPipeline.Classes
{
    public class CyberSource : ICyberSource
    {
        #region Properties

        private CyberSourceClient.RequestMessage _requestMessage;
        private CyberSourceClient.ReplyMessage _replyMessage;
        private CyberSourceClient.Card _creditCard;
        private CyberSourceClient.PurchaseTotals _purchaseTotals;
        private CyberSourceClient.BillTo _billTo;
        private StringBuilder sb = new StringBuilder();

        #endregion

        #region Constructors

        public CyberSource()
        {
            try
            {
                _requestMessage = new CyberSourceClient.RequestMessage()
                {
                    clientLibrary = ConfigurationManager.AppSettings["clientLibrary"],
                    clientLibraryVersion = Environment.Version.ToString(),
                    clientEnvironment =
                        Environment.OSVersion.Platform +
                        Environment.OSVersion.Version.ToString(),
                    billTo = new CyberSourceClient.BillTo(),
                    card = new CyberSourceClient.Card(),
                    purchaseTotals = new CyberSourceClient.PurchaseTotals()
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Exception" + ex.Message + "\n" + ex.StackTrace);
            }

        }

        #endregion

        #region Mapping methods

        public void MapCreditCard(CreditCard creditCard)
        {
            try
            {
                _creditCard = new CyberSourceClient.Card();

                if (!String.IsNullOrEmpty(creditCard.Number))
                {
                    _creditCard.accountNumber = creditCard.Number;
                }

                _creditCard.expirationMonth = creditCard.Month.ToString();
                _creditCard.expirationYear = creditCard.Year.ToString();

                if (_requestMessage.card != null)
                {
                    _requestMessage.card = _creditCard;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("<HR>" + ex.Message + "<BR>" + ex.StackTrace + "<HR>");
            }
        }

        public void MapPurchaseOrder(PurchaseOrder purchaseOrder)
        {
            try
            {
                _purchaseTotals = new CyberSourceClient.PurchaseTotals();

                //if we need to check a reject response code from CyberSource, we can toggle it on with this logic
                var triggerRejectResponse = Convert.ToBoolean(AppSettingsCheck.AppSettings("triggerRejectCode"));

                if (purchaseOrder.Amount != null && !triggerRejectResponse)
                {
                    _purchaseTotals.grandTotalAmount = purchaseOrder.Amount.ToString();
                }
                else
                {
                    _purchaseTotals.grandTotalAmount = AppSettingsCheck.AppSettings("triggerRejectAmount");
                }

                if (purchaseOrder.Freight != null)
                {
                    _purchaseTotals.freightAmount = purchaseOrder.Freight.ToString();
                }

                if (purchaseOrder.Tax != null)
                {
                    _purchaseTotals.taxAmount = purchaseOrder.Tax.ToString();
                }

                _purchaseTotals.currency = "USD";


                if (!String.IsNullOrEmpty(purchaseOrder.PO))
                {
                    _requestMessage.merchantReferenceCode = purchaseOrder.PO;
                }

                if (_requestMessage.purchaseTotals != null)
                {
                    _requestMessage.purchaseTotals = _purchaseTotals;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Exception" + ex.Message + "\n" + ex.StackTrace + "<HR>");
            }
        }

        public void MapBillingInfo(Address address)
        {
            try
            {
                _billTo = new CyberSourceClient.BillTo();

                if (!String.IsNullOrEmpty(address.FName))
                {
                    _billTo.firstName = address.FName;
                }

                if (!String.IsNullOrEmpty(address.LName))
                {
                    _billTo.lastName = address.LName;
                }

                if (!String.IsNullOrEmpty(address.Street))
                {
                    _billTo.street1 = address.Street;
                }

                if (!String.IsNullOrEmpty(address.City))
                {
                    _billTo.city = address.City;
                }

                if (!String.IsNullOrEmpty(address.State))
                {
                    _billTo.state = address.State;
                }

                if (!String.IsNullOrEmpty(address.Zip))
                {
                    _billTo.postalCode = address.Zip;
                }

                _billTo.country = address.Country;


                if (!String.IsNullOrEmpty(address.Email))
                {
                    _billTo.email = address.Email;
                }

                if (_requestMessage.billTo != null)
                {
                    _requestMessage.billTo = _billTo;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Exception" + ex.Message + "\n" + ex.StackTrace);
            }
        }

        #endregion

        #region Void methods

        public string VoidCreditCardTransaction(string requestId, string merchantReferenceCode, string siteName)
        {
            try
            {
                string _siteName = siteName;
                string _merchantId = "";
                string _transactionKey = "";

                sb.Append("CyberSource Class:\r\n");

                //call to get correct merchant id based on site name
                if (!String.IsNullOrEmpty(_siteName))
                {
                    _merchantId = Util.ConvertSiteNameToMerchantId(_siteName);
                }

                //call to get transaction key based on site name
                if (!String.IsNullOrEmpty(_siteName))
                {
                    _transactionKey = Util.GetTransactionKeyBySiteName(_siteName);
                }

                sb.Append("SiteName: " + _siteName + "\r\n");
                sb.Append("Merchant ID = " + _merchantId + "\r\n");

                var transaction_key = AppSettingsCheck.AppSettings("transaction_key");
                var address = AppSettingsCheck.AppSettings("endpoint");
                var _requestId = requestId;
                var _merchantReferenceCode = merchantReferenceCode;

                var binding = new BasicHttpBinding(BasicHttpSecurityMode.TransportWithMessageCredential);
                binding.ReaderQuotas.MaxArrayLength = Int32.MaxValue;
                binding.ReaderQuotas.MaxBytesPerRead = Int32.MaxValue;
                binding.ReaderQuotas.MaxDepth = Int32.MaxValue;
                binding.ReaderQuotas.MaxNameTableCharCount = Int32.MaxValue;
                binding.ReaderQuotas.MaxStringContentLength = Int32.MaxValue;

                var endpoint = new EndpointAddress(address);
                var proc = new CyberSourceClient.TransactionProcessorClient(binding, endpoint);
                _replyMessage = new CyberSourceClient.ReplyMessage();

                if (_requestMessage != null)
                {
                    if (!String.IsNullOrEmpty(_merchantId))
                    {

                    TODO: //Before doing a void call, we need to see if the merchant reference code exists within CyberSource table.
                        //If the reference code doesn't exist, throw an exception that prevents the void from completing on the site.
                        _requestMessage.merchantID = _merchantId;
                        _requestMessage.merchantReferenceCode = _merchantReferenceCode;
                        _requestMessage.voidService = new CyberSourceClient.VoidService
                        {
                            run = "true",
                            voidRequestID = _requestId
                        };
                    }

                    //assign merchant id
                    _requestMessage.merchantID = _merchantId;

                    if (proc.ChannelFactory.Credentials != null && !String.IsNullOrEmpty(_merchantId))
                    {
                        proc.ChannelFactory.Credentials.UserName.UserName = _merchantId;
                    }

                    if (proc.ChannelFactory.Credentials != null && !String.IsNullOrEmpty(_transactionKey))
                    {
                        proc.ChannelFactory.Credentials.UserName.Password = _transactionKey;
                    }

                    if (_replyMessage != null && proc != null)
                    {
                        //output void request value right before calling void
                        sb.Append("void request object right before calling cybersource void\r\n");
                        sb.Append("MerchantID = " + _requestMessage.merchantID + "\r\n RequestID = " + _requestMessage.voidService.voidRequestID + "\r\n");

                        _replyMessage = proc.runTransaction(_requestMessage);
                        sb.Append("Reply message from calling void. Void reply reason code: " + _replyMessage.voidReply.reasonCode + "\r\n");
                        TraceToVoidLog(sb.ToString());
                    }
                }
            }
            catch (TimeoutException e)
            {
                throw new TimeoutException("TimeoutException: " + e.Message + "\n" + e.StackTrace);
            }
            catch (SystemException e)
            {
                throw new SystemException("FaultException: " + e.Message + "\n" + e.StackTrace);
            }

            return _replyMessage.voidReply.reasonCode;
        }

        #endregion

        #region Request and Response methods

        /// <summary>
        /// SendRequest constructs a CyberSource request object with 
        /// required parameters, and makes an authentication request to
        /// CyberSource. The reply message contains authorization code, token,
        /// and merchant request id
        /// </summary>
        /// <param name="siteName"></param>
        /// <returns></returns>
        public CyberSourceClient.ReplyMessage SendRequest(string siteName)
        {
            try
            {
                string _siteName = siteName;
                string _merchantId = "";
                string _transactionKey = "";

                sb.Append("CyberSource Class:\r\n");

                //call to get correct merchant id based on site name
                if (!String.IsNullOrEmpty(_siteName))
                {
                    _merchantId = Util.ConvertSiteNameToMerchantId(_siteName);
                }

                //call to get transaction key based on site name
                if (!String.IsNullOrEmpty(_siteName))
                {
                    _transactionKey = Util.GetTransactionKeyBySiteName(_siteName);
                }

                sb.Append("SiteName: " + _siteName + "\r\n");
                sb.Append("Merchant ID = " + _merchantId + "\r\n");

                TraceToLog(sb.ToString());

                var address = AppSettingsCheck.AppSettings("endpoint");

                var binding = new BasicHttpBinding(BasicHttpSecurityMode.TransportWithMessageCredential);
                binding.ReaderQuotas.MaxArrayLength = Int32.MaxValue;
                binding.ReaderQuotas.MaxBytesPerRead = Int32.MaxValue;
                binding.ReaderQuotas.MaxDepth = Int32.MaxValue;
                binding.ReaderQuotas.MaxNameTableCharCount = Int32.MaxValue;
                binding.ReaderQuotas.MaxStringContentLength = Int32.MaxValue;

                var endpoint = new EndpointAddress(address);
                var proc = new CyberSourceClient.TransactionProcessorClient(binding, endpoint);
                _replyMessage = new CyberSourceClient.ReplyMessage();

                //Authorization service setting
                _requestMessage.ccAuthService = new CyberSourceClient.CCAuthService();
                _requestMessage.ccAuthService.run = "true";

                //assign merchant id
                _requestMessage.merchantID = _merchantId;

                //toggle for ignoring the AVS response

                sb.Append("Entering AVS ignoring code block\r\n");
                // var avsResponse = true//ConfigurationManager.AppSettings["AVSResponse"];
                _requestMessage.businessRules = new CyberSourceClient.BusinessRules();

                if (_requestMessage.businessRules != null)
                {
                    _requestMessage.businessRules.ignoreAVSResult = "true";
                    sb.Append("Ignoring AVS response.");
                    TraceToLog(sb.ToString());
                }

                if (proc.ChannelFactory.Credentials != null && !String.IsNullOrEmpty(_merchantId))
                {
                    proc.ChannelFactory.Credentials.UserName.UserName = _merchantId;
                }

                if (proc.ChannelFactory.Credentials != null && !String.IsNullOrEmpty(_transactionKey))
                {
                    proc.ChannelFactory.Credentials.UserName.Password = _transactionKey;
                }

                if (_replyMessage != null && proc != null)
                {
                    _replyMessage = proc.runTransaction(_requestMessage);
                }

                return _replyMessage;
            }
            catch (TimeoutException e)
            {
                throw new TimeoutException("TimeoutException: " + e.Message + "\n" + e.StackTrace);
            }
            catch (SystemException e)
            {
                throw new SystemException("FaultException: " + e.Message + "\n" + e.StackTrace);
            }
        }

        #endregion

        private void TraceToLog(string message)
        {
            StreamWriter writer =
                new StreamWriter("C:\\PMT\\cyber_source_class" + DateTime.Now.ToString("yyyy-MM-dd") + ".log", true);

            try
            {
                writer.WriteLine(message);
                writer.Flush();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (writer != null)
                {
                    writer.Close();
                    writer.Dispose();
                }
            }
        }

        private void TraceToVoidLog(string message)
        {
            StreamWriter writer =
                new StreamWriter("C:\\PMT\\cyber_source_void" + DateTime.Now.ToString("yyyy-MM-dd") + ".log", true);

            try
            {
                writer.WriteLine(message);
                writer.Flush();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (writer != null)
                {
                    writer.Close();
                    writer.Dispose();
                }
            }
        }
    }
}
