using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Net;
using System.IO;
using System.Web;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CommerceGeneration.MerchantServicesPipeline.Interfaces;
using Microsoft.CommerceServer.Runtime;
using Microsoft.CommerceServer.Interop;
using Microsoft.CommerceServer.Interop.Orders;
using Microsoft.CommerceServer.Runtime.Orders;
using CommerceGeneration.MerchantServicesPipeline.Constants;
using AuthorizeNetKeys = CommerceGeneration.MerchantServicesPipeline.Constants.AuthorizeNet;
using PaymentTechKeys = CommerceGeneration.MerchantServicesPipeline.Constants.PaymentTech;
using System.Configuration;
using CommerceGeneration.MerchantServicesPipeline.CyberSourceServiceClient;
using CommerceGeneration.MerchantServicesPipeline.Classes;

namespace CommerceGeneration.MerchantServicesPipeline
{

    [ComVisible(true)]

    public class ProcessPayment : IPipelineComponent, IPipelineComponentAdmin, IPipelineComponentDescription,
                                  IPersistDictionary, ISpecifyPipelineComponentUI
    {
        private const Int32 StatusSuccess = 1;
        private const Int32 StatusWarning = 2;
        private const Int32 StatusError = 3;

        private Providers _Provider;

        #region CyberSource variables

        private RequestMessage cyberSourceRequest;
        private ReplyMessage cyberSourceResponse;
        private ICyberSource cyberSource;

        #endregion


        #region CyberSource

        //For Cybersource: save merchant reference code, request id, and merchant id into a lookup table for void functionality. djk 3.21.14
        public void SaveCyberSourceResponse(PurchaseOrder order, ReplyMessage response, string siteName)
        {
            var sb = new StringBuilder();

            try
            {
                var requestId = "";
                var merchantId = "";
                var merchantReferenceCode = "";
                var _siteName = siteName;

                sb.Append("Entering save reponse\r\n");

                //call to get correct merchant id based on site name
                if (!String.IsNullOrEmpty(_siteName))
                {
                    merchantId = Util.ConvertSiteNameToMerchantId(_siteName);
                }

                //merchant reference code
                if (!string.IsNullOrEmpty(order.PO))
                {
                    merchantReferenceCode = order.PO;
                }

                sb.Append("Save response site name: " + _siteName + "\r\n");
                sb.Append("Save response merchant ID = " + merchantId + "\r\n");

                //request id
                if (!string.IsNullOrEmpty(response.requestID))
                {
                    requestId = response.requestID;
                }

                if (!String.IsNullOrEmpty(ConfigurationManager.AppSettings["CyberSourceTransactionsConnectionString"]))
                {
                    var connectionString = ConfigurationManager.AppSettings["CyberSourceTransactionsConnectionString"];
                    var insertCybersourceResponse = "InsertCyberSourceResponse";

                    sb.Append("connection string: " + connectionString + "\r\n");

                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();

                        sb.Append("connection string open \r\n");

                        SqlParameter requestIdParam = new SqlParameter
                        {
                            ParameterName = "RequestId",
                            SqlDbType = SqlDbType.VarChar,
                            Direction = ParameterDirection.Input,
                            Value = requestId

                        };

                        SqlParameter merchantIdParam = new SqlParameter()
                        {
                            ParameterName = "MerchantId",
                            SqlDbType = SqlDbType.VarChar,
                            Direction = ParameterDirection.Input,
                            Value = merchantId
                        };

                        SqlParameter merchantReferenceCodeParam = new SqlParameter()
                        {
                            ParameterName =
                                "MerchantReferenceCode",
                            SqlDbType = SqlDbType.VarChar,
                            Direction = ParameterDirection.Input,
                            Value = merchantReferenceCode
                        };


                        if (conn != null)
                        {
                            SqlCommand command = new SqlCommand(insertCybersourceResponse, conn);
                            command.CommandType = CommandType.StoredProcedure;
                            command.Parameters.Add(requestIdParam);
                            command.Parameters.Add(merchantIdParam);
                            command.Parameters.Add(merchantReferenceCodeParam);

                            var result = command.ExecuteNonQuery();
                        }
                    }
                }
                else
                {
                    sb.Append("save response connection string from config is empty.");
                }

                TraceToLogSaveResponse(sb.ToString());
            }
            catch (ApplicationException e)
            {
                sb.Append("Error: " + e.Message);
                TraceToLogSaveResponse(sb.ToString());
                throw new Exception(e.Message);
            }
        }

        private Dictionary<string, string> GetCyberRequestId(string merchantReferenceCode)
        {
            var sb = new StringBuilder();

            try
            {
                var _merchantReferenceCode = merchantReferenceCode;
                var getCybersourceResponse = "GetCyberSourceResponse";

                var requestId = "";
                Dictionary<string, string> cyberRequestValues = new Dictionary<string, string>();


                if (!String.IsNullOrEmpty(ConfigurationManager.AppSettings["CyberSourceTransactionsConnectionString"]))
                {
                    var connectionString = ConfigurationManager.AppSettings["CyberSourceTransactionsConnectionString"];

                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();

                        SqlParameter merchantReferenceCodeParam = new SqlParameter
                        {
                            ParameterName = "MerchantReferenceCode",
                            SqlDbType = SqlDbType.VarChar,
                            Direction = ParameterDirection.Input,
                            Value = _merchantReferenceCode
                        };

                        SqlCommand command = new SqlCommand(getCybersourceResponse, conn);
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add(merchantReferenceCodeParam);

                        var reader = command.ExecuteReader();

                        while (reader.Read())
                        {
                            if (!String.IsNullOrEmpty(reader["RequestId"].ToString()))
                            {
                                requestId = reader["RequestId"].ToString();
                                cyberRequestValues.Add("requestId", requestId);
                            }
                        }

                        reader.Close();
                    }
                }
                else
                {
                    sb.Append("connection string from config is empty.");
                    TraceToLog(sb.ToString());
                }

                return cyberRequestValues;
            }
            catch (ApplicationException e)
            {
                sb.Append("Error: " + e.Message);
                TraceToLog(sb.ToString());
                throw new Exception(e.Message);
            }
        }

        private string GetEmailAddressForUser(string customerId)
        {
            var sb = new StringBuilder();
            var _customerId = "";
            var email = "";
            var sql = "";
            var connectionString = "";

            try
            {
                connectionString = ConfigurationManager.AppSettings["CyberSourceProfileConnectionString"];
                sb.Append("Connection string: " + connectionString);

                sql = "sp_GetEmailByUserId";

                if (!String.IsNullOrEmpty(customerId))
                {
                    _customerId = customerId;
                }

                sb.Append("Starting get email by userId\r\n");

                if (!String.IsNullOrEmpty(connectionString))
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();

                        SqlParameter userId = new SqlParameter
                        {
                            ParameterName = "UserId",
                            SqlDbType = SqlDbType.VarChar,
                            Direction = ParameterDirection.Input,
                            Value = _customerId
                        };

                        SqlCommand command = new SqlCommand(sql, conn);
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add(userId);

                        if (command != null)
                        {
                            var reader = command.ExecuteReader();

                            while (reader.Read())
                            {
                                if (!String.IsNullOrEmpty(reader[0].ToString()))
                                {
                                    email = reader[0].ToString();
                                }
                            }

                            reader.Close();
                        }
                    }
                }

                sb.Append("Returned email: " + email + "\r\n");
                return email;
            }
            catch (ApplicationException e)
            {
                sb.Append("Error: " + e.Message);
                TraceToLog(sb.ToString());
                throw new Exception(e.Message);
            }

        #endregion
        }
    }
}
