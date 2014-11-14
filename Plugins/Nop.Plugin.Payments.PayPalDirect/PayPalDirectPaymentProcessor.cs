using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Routing;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.PayPalDirect.Controllers;
using Nop.Plugin.Payments.PayPalDirect.PayPalSvc;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.PayPalDirect
{
    /// <summary>
    /// PayPalDirect payment processor
    /// </summary>
    public class PayPalDirectPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly PayPalDirectPaymentSettings _paypalDirectPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerService _customerService;
        private readonly CurrencySettings _currencySettings;
        private readonly IWebHelper _webHelper;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        #endregion

        #region Ctor

        public PayPalDirectPaymentProcessor(PayPalDirectPaymentSettings paypalDirectPaymentSettings,
            ISettingService settingService, 
            ICurrencyService currencyService, ICustomerService customerService,
            CurrencySettings currencySettings, IWebHelper webHelper, 
            IOrderTotalCalculationService orderTotalCalculationService)
        {
            this._paypalDirectPaymentSettings = paypalDirectPaymentSettings;
            this._settingService = settingService;
            this._currencyService = currencyService;
            this._customerService = customerService;
            this._currencySettings = currencySettings;
            this._webHelper = webHelper;
            this._orderTotalCalculationService = orderTotalCalculationService;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Gets Paypal URL
        /// </summary>
        /// <returns></returns>
        private string GetPaypalUrl()
        {
            return _paypalDirectPaymentSettings.UseSandbox ? "https://www.sandbox.paypal.com/us/cgi-bin/webscr" :
                "https://www.paypal.com/us/cgi-bin/webscr";
        }

        /// <summary>
        /// Get Paypal country code
        /// </summary>
        /// <param name="country">Country</param>
        /// <returns>Paypal country code</returns>
        protected CountryCodeType GetPaypalCountryCodeType(Country country)
        {
            CountryCodeType payerCountry = CountryCodeType.US;
            try
            {
                payerCountry = (CountryCodeType)Enum.Parse(typeof(CountryCodeType), country.TwoLetterIsoCode);
            }
            catch
            {
            }
            return payerCountry;
        }

        /// <summary>
        /// Get Paypal credit card type
        /// </summary>
        /// <param name="creditCardType">Credit card type</param>
        /// <returns>Paypal credit card type</returns>
        protected CreditCardTypeType GetPaypalCreditCardType(string creditCardType)
        {
            var creditCardTypeType = (CreditCardTypeType)Enum.Parse(typeof(CreditCardTypeType), creditCardType);
            return creditCardTypeType;
            //if (creditCardType.ToLower() == "visa")
            //    return CreditCardTypeType.Visa;

            //if (creditCardType.ToLower() == "mastercard")
            //    return CreditCardTypeType.MasterCard;

            //if (creditCardType.ToLower() == "americanexpress")
            //    return CreditCardTypeType.Amex;

            //if (creditCardType.ToLower() == "discover")
            //    return CreditCardTypeType.Discover;

            //throw new NopException("Unknown credit card type");
        }

        protected string GetApiVersion()
        {
            return "63";
        }

        protected ProcessPaymentResult AuthorizeOrSale(ProcessPaymentRequest processPaymentRequest, bool authorizeOnly)
        {
            var result = new ProcessPaymentResult();

            var customer = _customerService.GetCustomerById(processPaymentRequest.CustomerId);
            if (customer == null)
                throw new Exception("Customer cannot be loaded");

            var req = new DoDirectPaymentReq();
            req.DoDirectPaymentRequest = new DoDirectPaymentRequestType();
            req.DoDirectPaymentRequest.Version = GetApiVersion();
            var details = new DoDirectPaymentRequestDetailsType();
            req.DoDirectPaymentRequest.DoDirectPaymentRequestDetails = details;
            details.IPAddress = _webHelper.GetCurrentIpAddress() ?? "";
            if (authorizeOnly)
                details.PaymentAction = PaymentActionCodeType.Authorization;
            else
                details.PaymentAction = PaymentActionCodeType.Sale;
            //credit card
            details.CreditCard = new CreditCardDetailsType();
            details.CreditCard.CreditCardNumber = processPaymentRequest.CreditCardNumber;
            details.CreditCard.CreditCardType = GetPaypalCreditCardType(processPaymentRequest.CreditCardType);
            details.CreditCard.ExpMonthSpecified = true;
            details.CreditCard.ExpMonth = processPaymentRequest.CreditCardExpireMonth;
            details.CreditCard.ExpYearSpecified = true;
            details.CreditCard.ExpYear = processPaymentRequest.CreditCardExpireYear;
            details.CreditCard.CVV2 = processPaymentRequest.CreditCardCvv2;
            details.CreditCard.CardOwner = new PayerInfoType();
            details.CreditCard.CardOwner.PayerCountry = GetPaypalCountryCodeType(customer.BillingAddress.Country);
            details.CreditCard.CreditCardTypeSpecified = true;
            //billing address
            details.CreditCard.CardOwner.Address = new AddressType();
            details.CreditCard.CardOwner.Address.CountrySpecified = true;
            details.CreditCard.CardOwner.Address.Street1 = customer.BillingAddress.Address1;
            details.CreditCard.CardOwner.Address.Street2 = customer.BillingAddress.Address2;
            details.CreditCard.CardOwner.Address.CityName = customer.BillingAddress.City;
            if (customer.BillingAddress.StateProvince != null)
                details.CreditCard.CardOwner.Address.StateOrProvince = customer.BillingAddress.StateProvince.Abbreviation;
            else
                details.CreditCard.CardOwner.Address.StateOrProvince = "CA";
            details.CreditCard.CardOwner.Address.Country = GetPaypalCountryCodeType(customer.BillingAddress.Country);
            details.CreditCard.CardOwner.Address.PostalCode = customer.BillingAddress.ZipPostalCode;
            details.CreditCard.CardOwner.Payer = customer.BillingAddress.Email;
            details.CreditCard.CardOwner.PayerName = new PersonNameType();
            details.CreditCard.CardOwner.PayerName.FirstName = customer.BillingAddress.FirstName;
            details.CreditCard.CardOwner.PayerName.LastName = customer.BillingAddress.LastName;
            //order totals
            var payPalCurrency = PaypalHelper.GetPaypalCurrency(_currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId));
            details.PaymentDetails = new PaymentDetailsType();
            details.PaymentDetails.OrderTotal = new BasicAmountType();
            details.PaymentDetails.OrderTotal.Value = Math.Round(processPaymentRequest.OrderTotal, 2).ToString("N", new CultureInfo("en-us"));
            details.PaymentDetails.OrderTotal.currencyID = payPalCurrency;
            details.PaymentDetails.Custom = processPaymentRequest.OrderGuid.ToString();
            details.PaymentDetails.ButtonSource = "nopCommerceCart";
            //pass product names and totals to PayPal
            //if (_paypalDirectPaymentSettings.PassProductNamesAndTotals)
            //{
            //    //individual items
                //var cart = customer.ShoppingCartItems
                //    .Where(x=>x.ShoppingCartType == ShoppingCartType.ShoppingCart)
                //    .LimitPerStore(processPaymentRequest.StoreId)
                //    .ToList();
            //    var cartItems = new PaymentDetailsItemType[cart.Count];
            //    for (int i = 0; i < cart.Count; i++)
            //    {
            //        var sc = cart[i];
            //        decimal taxRate = decimal.Zero;
            //        var customer = processPaymentRequest.Customer;
            //        decimal scUnitPrice = _priceCalculationService.GetUnitPrice(sc, true);
            //        decimal scSubTotal = _priceCalculationService.GetSubTotal(sc, true);
            //        decimal scUnitPriceInclTax = _taxService.GetProductPrice(sc.ProductVariant, scUnitPrice, true, customer, out taxRate);
            //        decimal scUnitPriceExclTax = _taxService.GetProductPrice(sc.ProductVariant, scUnitPrice, false, customer, out taxRate);
            //        //decimal scSubTotalInclTax = _taxService.GetProductPrice(sc.ProductVariant, scSubTotal, true, customer, out taxRate);
            //        //decimal scSubTotalExclTax = _taxService.GetProductPrice(sc.ProductVariant, scSubTotal, false, customer, out taxRate);
            //        cartItems[i] = new PaymentDetailsItemType()
            //        {
            //            Name = sc.ProductVariant.FullProductName,
            //            Number = sc.ProductVariant.Id.ToString(),
            //            Quantity = sc.Quantity.ToString(),
            //            Amount = new BasicAmountType()
            //            {
            //                currencyID = payPalCurrency,
            //                Value = scUnitPriceExclTax.ToString("N", new CultureInfo("en-us")),
            //            },
            //            Tax = new BasicAmountType()
            //            {
            //                currencyID = payPalCurrency,
            //                Value = (scUnitPriceInclTax - scUnitPriceExclTax).ToString("N", new CultureInfo("en-us")),
            //            },
            //        };
            //    };
            //    details.PaymentDetails.PaymentDetailsItem = cartItems;
            //    //other totals (undone)
            //    details.PaymentDetails.ItemTotal = null;
            //    details.PaymentDetails.ShippingTotal = null;
            //    details.PaymentDetails.TaxTotal = null;
            //    details.PaymentDetails.HandlingTotal = null;
            //}
            //shipping
            if (customer.ShippingAddress != null)
            {
                if (customer.ShippingAddress.StateProvince != null && customer.ShippingAddress.Country != null)
                {
                    var shippingAddress = new AddressType();
                    shippingAddress.Name = customer.ShippingAddress.FirstName + " " + customer.ShippingAddress.LastName;
                    shippingAddress.Street1 = customer.ShippingAddress.Address1;
                    shippingAddress.CityName = customer.ShippingAddress.City;
                    shippingAddress.StateOrProvince = customer.ShippingAddress.StateProvince.Abbreviation;
                    shippingAddress.PostalCode = customer.ShippingAddress.ZipPostalCode;
                    shippingAddress.Country = (CountryCodeType)Enum.Parse(typeof(CountryCodeType), customer.ShippingAddress.Country.TwoLetterIsoCode, true);
                    shippingAddress.CountrySpecified = true;
                    details.PaymentDetails.ShipToAddress = shippingAddress;
                }
            }

            //send request
            using (var service2 = new PayPalAPIAASoapBinding())
            {
                if (!_paypalDirectPaymentSettings.UseSandbox)
                    service2.Url = "https://api-3t.paypal.com/2.0/";
                else
                    service2.Url = "https://api-3t.sandbox.paypal.com/2.0/";

                service2.RequesterCredentials = new CustomSecurityHeaderType();
                service2.RequesterCredentials.Credentials = new UserIdPasswordType();
                service2.RequesterCredentials.Credentials.Username = _paypalDirectPaymentSettings.ApiAccountName;
                service2.RequesterCredentials.Credentials.Password = _paypalDirectPaymentSettings.ApiAccountPassword;
                service2.RequesterCredentials.Credentials.Signature = _paypalDirectPaymentSettings.Signature;
                service2.RequesterCredentials.Credentials.Subject = "";

                DoDirectPaymentResponseType response = service2.DoDirectPayment(req);

                string error = "";
                bool success = PaypalHelper.CheckSuccess(response, out error);
                if (success)
                {
                    result.AvsResult = response.AVSCode;
                    result.AuthorizationTransactionCode = response.CVV2Code;
                    if (authorizeOnly)
                    {
                        result.AuthorizationTransactionId = response.TransactionID;
                        result.AuthorizationTransactionResult = response.Ack.ToString();

                        result.NewPaymentStatus = PaymentStatus.Authorized;
                    }
                    else
                    {
                        result.CaptureTransactionId = response.TransactionID;
                        result.CaptureTransactionResult = response.Ack.ToString();

                        result.NewPaymentStatus = PaymentStatus.Paid;
                    }
                }
                else
                {
                    result.AddError(error);
                }
            }
            return result;
        }

        /// <summary>
        /// Verifies IPN
        /// </summary>
        /// <param name="formString">Form string</param>
        /// <param name="values">Values</param>
        /// <returns>Result</returns>
        public bool VerifyIPN(string formString, out Dictionary<string, string> values)
        {
            var req = (HttpWebRequest)WebRequest.Create(GetPaypalUrl());
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";

            string formContent = string.Format("{0}&cmd=_notify-validate", formString);
            req.ContentLength = formContent.Length;

            using (var sw = new StreamWriter(req.GetRequestStream(), Encoding.ASCII))
            {
                sw.Write(formContent);
            }

            string response = null;
            using (var sr = new StreamReader(req.GetResponse().GetResponseStream()))
            {
                response = HttpUtility.UrlDecode(sr.ReadToEnd());
            }
            bool success = response.Trim().Equals("VERIFIED", StringComparison.OrdinalIgnoreCase);

            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string l in formString.Split('&'))
            {
                string line = l.Trim();
                int equalPox = line.IndexOf('=');
                if (equalPox >= 0)
                    values.Add(line.Substring(0, equalPox), line.Substring(equalPox + 1));
            }

            return success;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            if (_paypalDirectPaymentSettings.TransactMode == TransactMode.Authorize)
            {
                return AuthorizeOrSale(processPaymentRequest, true);
            }
            else
            {
                return AuthorizeOrSale(processPaymentRequest, false);
            }
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
                _paypalDirectPaymentSettings.AdditionalFee, _paypalDirectPaymentSettings.AdditionalFeePercentage);
            return result;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();

            string authorizationId = capturePaymentRequest.Order.AuthorizationTransactionId;
            var req = new DoCaptureReq();
            req.DoCaptureRequest = new DoCaptureRequestType();
            req.DoCaptureRequest.Version = GetApiVersion();
            req.DoCaptureRequest.AuthorizationID = authorizationId;
            req.DoCaptureRequest.Amount = new BasicAmountType();
            req.DoCaptureRequest.Amount.Value = Math.Round(capturePaymentRequest.Order.OrderTotal, 2).ToString("N", new CultureInfo("en-us"));
            req.DoCaptureRequest.Amount.currencyID = PaypalHelper.GetPaypalCurrency(_currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId));
            req.DoCaptureRequest.CompleteType = CompleteCodeType.Complete;

            using (var service2 = new PayPalAPIAASoapBinding())
            {
                if (!_paypalDirectPaymentSettings.UseSandbox)
                    service2.Url = "https://api-3t.paypal.com/2.0/";
                else
                    service2.Url = "https://api-3t.sandbox.paypal.com/2.0/";

                service2.RequesterCredentials = new CustomSecurityHeaderType();
                service2.RequesterCredentials.Credentials = new UserIdPasswordType();
                service2.RequesterCredentials.Credentials.Username = _paypalDirectPaymentSettings.ApiAccountName;
                service2.RequesterCredentials.Credentials.Password = _paypalDirectPaymentSettings.ApiAccountPassword;
                service2.RequesterCredentials.Credentials.Signature = _paypalDirectPaymentSettings.Signature;
                service2.RequesterCredentials.Credentials.Subject = "";

                DoCaptureResponseType response = service2.DoCapture(req);

                string error = "";
                bool success = PaypalHelper.CheckSuccess(response, out error);
                if (success)
                {
                    result.NewPaymentStatus = PaymentStatus.Paid;
                    result.CaptureTransactionId = response.DoCaptureResponseDetails.PaymentInfo.TransactionID;
                    result.CaptureTransactionResult = response.Ack.ToString();
                }
                else
                {
                    result.AddError(error);
                }
            }
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();

            string transactionId = refundPaymentRequest.Order.CaptureTransactionId;

            var req = new RefundTransactionReq();
            req.RefundTransactionRequest = new RefundTransactionRequestType();
            //NOTE: Specify amount in partial refund
            req.RefundTransactionRequest.RefundType = RefundType.Full;
            req.RefundTransactionRequest.RefundTypeSpecified = true;
            req.RefundTransactionRequest.Version = GetApiVersion();
            req.RefundTransactionRequest.TransactionID = transactionId;

            using (var service1 = new PayPalAPISoapBinding())
            {
                if (!_paypalDirectPaymentSettings.UseSandbox)
                    service1.Url = "https://api-3t.paypal.com/2.0/";
                else
                    service1.Url = "https://api-3t.sandbox.paypal.com/2.0/";

                service1.RequesterCredentials = new CustomSecurityHeaderType();
                service1.RequesterCredentials.Credentials = new UserIdPasswordType();
                service1.RequesterCredentials.Credentials.Username = _paypalDirectPaymentSettings.ApiAccountName;
                service1.RequesterCredentials.Credentials.Password = _paypalDirectPaymentSettings.ApiAccountPassword;
                service1.RequesterCredentials.Credentials.Signature = _paypalDirectPaymentSettings.Signature;
                service1.RequesterCredentials.Credentials.Subject = "";

                RefundTransactionResponseType response = service1.RefundTransaction(req);

                string error = string.Empty;
                bool Success = PaypalHelper.CheckSuccess(response, out error);
                if (Success)
                {
                    result.NewPaymentStatus = PaymentStatus.Refunded;
                    //cancelPaymentResult.RefundTransactionID = response.RefundTransactionID;
                }
                else
                {
                    result.AddError(error);
                }
            }

            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();

            string transactionId = voidPaymentRequest.Order.AuthorizationTransactionId;
            if (String.IsNullOrEmpty(transactionId))
                transactionId = voidPaymentRequest.Order.CaptureTransactionId;

            var req = new DoVoidReq();
            req.DoVoidRequest = new DoVoidRequestType();
            req.DoVoidRequest.Version = GetApiVersion();
            req.DoVoidRequest.AuthorizationID = transactionId;


            using (var service2 = new PayPalAPIAASoapBinding())
            {
                if (!_paypalDirectPaymentSettings.UseSandbox)
                    service2.Url = "https://api-3t.paypal.com/2.0/";
                else
                    service2.Url = "https://api-3t.sandbox.paypal.com/2.0/";

                service2.RequesterCredentials = new CustomSecurityHeaderType();
                service2.RequesterCredentials.Credentials = new UserIdPasswordType();
                service2.RequesterCredentials.Credentials.Username = _paypalDirectPaymentSettings.ApiAccountName;
                service2.RequesterCredentials.Credentials.Password = _paypalDirectPaymentSettings.ApiAccountPassword;
                service2.RequesterCredentials.Credentials.Signature = _paypalDirectPaymentSettings.Signature;
                service2.RequesterCredentials.Credentials.Subject = "";

                DoVoidResponseType response = service2.DoVoid(req);

                string error = "";
                bool success = PaypalHelper.CheckSuccess(response, out error);
                if (success)
                {
                    result.NewPaymentStatus = PaymentStatus.Voided;
                    //result.VoidTransactionID = response.RefundTransactionID;
                }
                else
                {
                    result.AddError(error);
                }
            }
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();

            var customer = _customerService.GetCustomerById(processPaymentRequest.CustomerId);

            var req = new CreateRecurringPaymentsProfileReq();
            req.CreateRecurringPaymentsProfileRequest = new CreateRecurringPaymentsProfileRequestType();
            req.CreateRecurringPaymentsProfileRequest.Version = GetApiVersion();
            var details = new CreateRecurringPaymentsProfileRequestDetailsType();
            req.CreateRecurringPaymentsProfileRequest.CreateRecurringPaymentsProfileRequestDetails = details;

            details.CreditCard = new CreditCardDetailsType();
            details.CreditCard.CreditCardNumber = processPaymentRequest.CreditCardNumber;
            details.CreditCard.CreditCardType = GetPaypalCreditCardType(processPaymentRequest.CreditCardType);
            details.CreditCard.ExpMonthSpecified = true;
            details.CreditCard.ExpMonth = processPaymentRequest.CreditCardExpireMonth;
            details.CreditCard.ExpYearSpecified = true;
            details.CreditCard.ExpYear = processPaymentRequest.CreditCardExpireYear;
            details.CreditCard.CVV2 = processPaymentRequest.CreditCardCvv2;
            details.CreditCard.CardOwner = new PayerInfoType();
            details.CreditCard.CardOwner.PayerCountry = GetPaypalCountryCodeType(customer.BillingAddress.Country);
            details.CreditCard.CreditCardTypeSpecified = true;

            details.CreditCard.CardOwner.Address = new AddressType();
            details.CreditCard.CardOwner.Address.CountrySpecified = true;
            details.CreditCard.CardOwner.Address.Street1 = customer.BillingAddress.Address1;
            details.CreditCard.CardOwner.Address.Street2 = customer.BillingAddress.Address2;
            details.CreditCard.CardOwner.Address.CityName = customer.BillingAddress.City;
            if (customer.BillingAddress.StateProvince != null)
                details.CreditCard.CardOwner.Address.StateOrProvince = customer.BillingAddress.StateProvince.Abbreviation;
            else
                details.CreditCard.CardOwner.Address.StateOrProvince = "CA";
            details.CreditCard.CardOwner.Address.Country = GetPaypalCountryCodeType(customer.BillingAddress.Country);
            details.CreditCard.CardOwner.Address.PostalCode = customer.BillingAddress.ZipPostalCode;
            details.CreditCard.CardOwner.Payer = customer.BillingAddress.Email;
            details.CreditCard.CardOwner.PayerName = new PersonNameType();
            details.CreditCard.CardOwner.PayerName.FirstName = customer.BillingAddress.FirstName;
            details.CreditCard.CardOwner.PayerName.LastName = customer.BillingAddress.LastName;

            //start date
            details.RecurringPaymentsProfileDetails = new RecurringPaymentsProfileDetailsType();
            details.RecurringPaymentsProfileDetails.BillingStartDate = DateTime.UtcNow;
            details.RecurringPaymentsProfileDetails.ProfileReference = processPaymentRequest.OrderGuid.ToString();

            //schedule
            details.ScheduleDetails = new ScheduleDetailsType();
            details.ScheduleDetails.Description = "Recurring payment";
            details.ScheduleDetails.PaymentPeriod = new BillingPeriodDetailsType();
            details.ScheduleDetails.PaymentPeriod.Amount = new BasicAmountType();
            details.ScheduleDetails.PaymentPeriod.Amount.Value = Math.Round(processPaymentRequest.OrderTotal, 2).ToString("N", new CultureInfo("en-us"));
            details.ScheduleDetails.PaymentPeriod.Amount.currencyID = PaypalHelper.GetPaypalCurrency(_currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId));
            details.ScheduleDetails.PaymentPeriod.BillingFrequency = processPaymentRequest.RecurringCycleLength;
            switch (processPaymentRequest.RecurringCyclePeriod)
            {
                case RecurringProductCyclePeriod.Days:
                    details.ScheduleDetails.PaymentPeriod.BillingPeriod = BillingPeriodType.Day;
                    break;
                case RecurringProductCyclePeriod.Weeks:
                    details.ScheduleDetails.PaymentPeriod.BillingPeriod = BillingPeriodType.Week;
                    break;
                case RecurringProductCyclePeriod.Months:
                    details.ScheduleDetails.PaymentPeriod.BillingPeriod = BillingPeriodType.Month;
                    break;
                case RecurringProductCyclePeriod.Years:
                    details.ScheduleDetails.PaymentPeriod.BillingPeriod = BillingPeriodType.Year;
                    break;
                default:
                    throw new NopException("Not supported cycle period");
            }
            details.ScheduleDetails.PaymentPeriod.TotalBillingCycles = processPaymentRequest.RecurringTotalCycles;
            details.ScheduleDetails.PaymentPeriod.TotalBillingCyclesSpecified = true;



            using (var service2 = new PayPalAPIAASoapBinding())
            {
                if (!_paypalDirectPaymentSettings.UseSandbox)
                    service2.Url = "https://api-3t.paypal.com/2.0/";
                else
                    service2.Url = "https://api-3t.sandbox.paypal.com/2.0/";

                service2.RequesterCredentials = new CustomSecurityHeaderType();
                service2.RequesterCredentials.Credentials = new UserIdPasswordType();
                service2.RequesterCredentials.Credentials.Username = _paypalDirectPaymentSettings.ApiAccountName;
                service2.RequesterCredentials.Credentials.Password = _paypalDirectPaymentSettings.ApiAccountPassword;
                service2.RequesterCredentials.Credentials.Signature = _paypalDirectPaymentSettings.Signature;
                service2.RequesterCredentials.Credentials.Subject = "";

                CreateRecurringPaymentsProfileResponseType response = service2.CreateRecurringPaymentsProfile(req);

                string error = "";
                bool success = PaypalHelper.CheckSuccess(response, out error);
                if (success)
                {
                    result.NewPaymentStatus = PaymentStatus.Pending;
                    if (response.CreateRecurringPaymentsProfileResponseDetails != null)
                    {
                        result.SubscriptionTransactionId = response.CreateRecurringPaymentsProfileResponseDetails.ProfileID;
                    }
                }
                else
                {
                    result.AddError(error);
                }
            }

            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            var order = cancelPaymentRequest.Order;

            var req = new ManageRecurringPaymentsProfileStatusReq();
            req.ManageRecurringPaymentsProfileStatusRequest = new ManageRecurringPaymentsProfileStatusRequestType();
            req.ManageRecurringPaymentsProfileStatusRequest.Version = GetApiVersion();
            var details = new ManageRecurringPaymentsProfileStatusRequestDetailsType();
            req.ManageRecurringPaymentsProfileStatusRequest.ManageRecurringPaymentsProfileStatusRequestDetails = details;

            details.Action = StatusChangeActionType.Cancel;
            //Recurring payments profile ID returned in the CreateRecurringPaymentsProfile response
            details.ProfileID = order.SubscriptionTransactionId;
            
            using (var service2 = new PayPalAPIAASoapBinding())
            {
                if (!_paypalDirectPaymentSettings.UseSandbox)
                    service2.Url = "https://api-3t.paypal.com/2.0/";
                else
                    service2.Url = "https://api-3t.sandbox.paypal.com/2.0/";

                service2.RequesterCredentials = new CustomSecurityHeaderType();
                service2.RequesterCredentials.Credentials = new UserIdPasswordType();
                service2.RequesterCredentials.Credentials.Username = _paypalDirectPaymentSettings.ApiAccountName;
                service2.RequesterCredentials.Credentials.Password = _paypalDirectPaymentSettings.ApiAccountPassword;
                service2.RequesterCredentials.Credentials.Signature = _paypalDirectPaymentSettings.Signature;
                service2.RequesterCredentials.Credentials.Subject = "";

                var response = service2.ManageRecurringPaymentsProfileStatus(req);

                string error = "";
                if (!PaypalHelper.CheckSuccess(response, out error))
                {
                    result.AddError(error);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //it's not a redirection payment method. So we always return false
            return false;
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentPayPalDirect";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.PayPalDirect.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentPayPalDirect";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.PayPalDirect.Controllers" }, { "area", null } };
        }

        public Type GetControllerType()
        {
            return typeof(PaymentPayPalDirectController);
        }

        public override void Install()
        {
            //settings
            var settings = new PayPalDirectPaymentSettings()
            {
                TransactMode = TransactMode.Authorize,
                UseSandbox = true,
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.UseSandbox", "Use Sandbox");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.UseSandbox.Hint", "Check to enable Sandbox (testing environment).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.TransactMode", "Transaction mode");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.TransactMode.Hint", "Specify transaction mode.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.ApiAccountName", "API Account Name");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.ApiAccountName.Hint", "Specify API account name.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.ApiAccountPassword", "API Account Password");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.ApiAccountPassword.Hint", "Specify API account password.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.Signature", "Signature");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.Signature.Hint", "Specify signature.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.AdditionalFee", "Additional fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.AdditionalFeePercentage", "Additional fee. Use percentage");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
           
            base.Install();
        }
        
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PayPalDirectPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.UseSandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.UseSandbox.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.TransactMode");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.TransactMode.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.ApiAccountName");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.ApiAccountName.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.ApiAccountPassword");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.ApiAccountPassword.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.Signature");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.Signature.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalDirect.Fields.AdditionalFeePercentage.Hint");
           
            base.Uninstall();
        }

        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.Automatic;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Standard;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get
            {
                return false;
            }
        }

        #endregion
    }
}