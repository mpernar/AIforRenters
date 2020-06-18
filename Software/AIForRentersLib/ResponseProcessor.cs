﻿using AIForRentersLib.Exceptions;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using Microsoft.Recognizers.Text.Number;
using Org.BouncyCastle.Math.EC.Rfc7748;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIForRentersLib
{
    public static class ResponseProcessor
    {
        /// <summary>
        /// Receives instance (object) of ReceivedData class and doing semantic analysis on body of received email as a string.
        /// This method is using Microsoft.Recognizers.Text library for semantic analysis.
        /// </summary>
        /// <param name="receivedData"></param>
        /// <returns>
        /// New request object of class Request with its attributes.
        /// </returns>
        public static void ProcessData(List<ReceivedData> receivedData)
        {
            foreach (ReceivedData receivedDataItem in receivedData)
            {
                // Client e-mail adress
                string emailAddress  = receivedDataItem.EmailAddress;

                // Client e-mail subject
                string emailSubject = receivedDataItem.EmailSubject;

                // Client name and surname
                string emailSenderNameAndSurname = receivedDataItem.ClientNameSurname;
                string[] nameAndSurnameSplitted = emailSenderNameAndSurname.Split(' ');
                string name = nameAndSurnameSplitted[0];
                string surname = nameAndSurnameSplitted[1];

                //Email body
                string emailBody = receivedDataItem.EmailBody;

                // Processed email body data (Date and number of people)
                int numberOfPeople = 0;
                Property selectedProperty = null;
                Unit selectedUnit = null;
                DateTime dateFrom = DateTime.Now;
                DateTime dateTo = DateTime.Now;

                try
                {
                    numberOfPeople = ExtractNumberOfPeople(emailBody);
                    dateTo = CheckYear(DateTime.Parse(ExtractDateTo(emailBody, emailAddress)));
                    dateFrom = CheckYear(DateTime.Parse(ExtractDateFrom(emailBody, emailAddress)));
                }
                catch (Exception ex)
                {
                    if (ex is EmailContentException || ex is InvalidOperationException || ex is FormatException)
                    {
                        string subject = "Insufficient data";
                        string body = $"Dear {name}, \n\nwe are sorry to inform you that you have provided insufficient data in your email request! \nPlease resend your request with all necessary data! \n\nSincerely, \nAIForRenters";
                        EmailSender.SendEmail(subject, body, emailAddress);
                    }
                    return;
                }

                if (emailSubject == "Confirmation")
                {
                    ConfirmationEmail(nameAndSurnameSplitted, dateTo, dateFrom);
                }
                else
                {
                    try
                    {
                        selectedProperty = GetProperty(emailSubject);
                    }
                    catch (Exception ex)
                    {
                        if (ex is EmailContentException || ex is InvalidOperationException)
                        {
                            string subject = "Invalid property";
                            string body = $"Dear {name}, \n\nyou have sent invalid or nonexistent property name in email subject! \nPlease resend your request with valid property name in email subject! \n\nSincerely, \nAIForRenters";
                            EmailSender.SendEmail(subject, body, emailAddress);
                        }
                        return;
                    }

                    try
                    {
                        selectedUnit = GetUnit(emailSubject, numberOfPeople);
                    }
                    catch (Exception ex)
                    {
                        if (ex is EmailContentException || ex is InvalidOperationException)
                        {
                            string subject = "Unavailable unit";
                            string body = $"Dear {name}, \n\nwe are sorry to inform you that there are no available units that have a capacity for the number of people you requested! \n\nSincerely, \nAIForRenters";
                            EmailSender.SendEmail(subject, body, emailAddress);
                        }
                        return;
                    }

                    double priceUponRequest = selectedUnit.Price;
                    Client newClient = new Client()
                    {
                        Name = name,
                        Surname = surname,
                        Email = emailAddress
                    };

                    Request newRequest = new Request()
                    {
                        Property = selectedProperty.Name,
                        Unit = selectedUnit.Name,
                        FromDate = dateFrom,
                        ToDate = dateTo,
                        NumberOfPeople = numberOfPeople,
                        Client = newClient,
                        Confirmed = false,
                        Processed = false,
                        Sent = false,
                        PriceUponRequest = priceUponRequest,
                        ResponseSubject = "",
                        ResponseBody = ""
                    };

                    using (var context = new SE20E01_DBEntities())
                    {
                        context.Clients.Add(newClient);

                        context.Requests.Add(newRequest);

                        context.SaveChanges();
                    }
                }
            }
        }

        private static void ConfirmationEmail(string[] nameAndSurnameSplitted, DateTime dateTo, DateTime dateFrom)
        {
            string name = nameAndSurnameSplitted[0];
            string surname = nameAndSurnameSplitted[1];

            Request requestForConfirmation = null;
            using (var context = new SE20E01_DBEntities())
            {
                var query = from request in context.Requests
                            where request.Client.Name == name && request.Client.Surname == surname
                            && dateTo == request.ToDate && dateFrom == request.FromDate
                            select request;

                requestForConfirmation = query.Single();
            }

            requestForConfirmation.UpdateConfirmation(requestForConfirmation);
        }

        private static DateTime CheckYear(DateTime dateTime)
        {
            DateTime currentDateTime = DateTime.Now;

            if (dateTime.Year < currentDateTime.Year)
            {
                int diff = currentDateTime.Year - dateTime.Year;

                dateTime = dateTime.AddYears(diff);
                return dateTime;
            }
            else
            {
                return dateTime;
            }
            
        }

        private static Property GetProperty(string emailSubject)
        {
            Property selectedProperty;

            using (var context = new SE20E01_DBEntities())
            {
                var queryProperty = from property in context.Properties
                                where property.Name == emailSubject
                                select property;

                selectedProperty = queryProperty.Single();
            }
            return selectedProperty;
        }

        private static Unit GetUnit(string emailSubject, int numberOfPeople)
        {
            Unit selectedUnit;

            using (var context = new SE20E01_DBEntities())
            {
                var queryUnit = from unit in context.Units
                                where unit.Property.Name == emailSubject && unit.Capacity >= numberOfPeople
                                select unit;

                selectedUnit = queryUnit.First();
            }
            return selectedUnit;
        }

        private static int ExtractNumberOfPeople(string testEmailString)
        {
            var result = NumberRecognizer.RecognizeNumber(testEmailString, Culture.English);
            
            int.TryParse(result.First().Resolution["value"].ToString(), out int value);
            
            if (value.ToString() == null)
            {
                throw new EmailContentException("");
            }
            return value;
        }

        public static string ExtractDateFrom(string emailBody, string emailAddress)
        {
            string dateFrom = ExtractDates(emailBody).ToList<string>().ToArray()[2];

            if (dateFrom == null)
            {
                throw new EmailContentException("");
            }
            return dateFrom;
        }

        public static string ExtractDateTo(string emailBody, string emailAddress)
        {
            string dateTo = ExtractDates(emailBody).ToList<string>().ToArray()[3];

            if (dateTo == null)
            {
                throw new EmailContentException("");
            }
            return dateTo;
        }

        public static Dictionary<string, string>.ValueCollection ExtractDates(string emailBody)
        {
            if (emailBody == null)
            {
                throw new EmailContentException("");
            }
            var result = DateTimeRecognizer.RecognizeDateTime(emailBody, Culture.English);
            
            var extractFirstLayer = result.First().Resolution.Values.First() as List<Dictionary<string, string>>;
            
            var dictionaryAsValueCollection = extractFirstLayer.First().Values;
            return dictionaryAsValueCollection;
        }
    }
}
