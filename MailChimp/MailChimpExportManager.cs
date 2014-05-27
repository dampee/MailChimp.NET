﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ServiceStack.Text;
using MailChimp.Errors;
using MailChimp.Campaigns;

namespace MailChimp
{
	/// <summary>
	/// .NET API Wrapper for the Mailchimp Export v1.0 API.
	/// More information here: http://apidocs.mailchimp.com/export/1.0/
	/// </summary>
	public class MailChimpExportManager
	{
		#region Fields and properties

		/// <summary>
		/// The HTTPS endpoint for the API.  
		/// See http://apidocs.mailchimp.com/export/1.0/#api-endpoints for more information
		/// </summary>
		private string _httpsUrl = "https://{0}.api.mailchimp.com/export/1.0/{1}";

		/// <summary>
		/// The datacenter prefix.  This will be automatically determined
		/// based on your API key
		/// </summary>
		private string _dataCenterPrefix = string.Empty;

		#endregion

		#region Constructors and API key

		//  Default constructor
		public MailChimpExportManager()
		{
			// remove "__type" member from ServiceStack.Text JSON Serializer serialized strings
			JsConfig.ExcludeTypeInfo = true;
		}

		/// <summary>
		/// Get the datacenter prefix based on the API key passed
		/// </summary>
		/// <param name="apiKey">v2.0 Mailchimp API key</param>
		/// <returns></returns>
		private string GetDatacenterPrefix(string apiKey)
		{
			//  The key should contain a '-'.  If it doesn't, throw an exception:
			if (!apiKey.Contains('-'))
			{
				throw new ArgumentException("API key is not valid.  Must be a valid v2.0 Mailchimp API key.");
			}

			return apiKey.Split('-')[1];
		}

		/// <summary>
		/// Create an instance of the wrappper with your API key
		/// </summary>
		/// <param name="apiKey">The MailChimp API key to use</param>
		public MailChimpExportManager(string apiKey)
			: this()
		{
			this.APIKey = apiKey;
			this._dataCenterPrefix = GetDatacenterPrefix(apiKey);
		}

		/// <summary>
		/// Gets / sets your API key for all operations.  
		/// For help getting your API key, please see 
		/// http://kb.mailchimp.com/article/where-can-i-find-my-api-key
		/// </summary>
		public string APIKey
		{
			get;
			set;
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Formats a date parameter for the mailchimp API. Automatically converts to UTC time
		/// </summary>
		/// <param name="date"></param>
		/// <returns></returns>
		public static string ConvertDateTimeToUtcString(DateTime date)
		{
			double utcOffSet = DateTime.Now.Hour - DateTime.UtcNow.Hour;
			DateTime utcSyncTime = date.AddHours(-utcOffSet);
			String utcSyncString = utcSyncTime.ToString("yyyy-MM-dd HH:mm:ss");

			return utcSyncString;
		}

		static string ReturnFullUrlWithParameters(String url, object args)
		{
			string returnString = url;
			Dictionary<String, String> parameters = args.ToStringDictionary();
			List<string> keys = new List<string>();
			List<string> values = new List<string>();
			
			foreach(var pair in parameters)
			{
				keys.Add(pair.Key);
				values.Add(pair.Value);
			}

			for (int i = 0; i < parameters.Count; i++)
			{
				if (i == 0)
				{
					returnString += "?" + keys[i] + "=" + values[i];
				}
				else
				{
					returnString += "&" + keys[i] + "=" + values[i];
				}
			}

			return returnString;
		}

		static List<Dictionary<string, string>> ParseExportApiResults(string stringFromServer)
		{
			List<Dictionary<string, string>> returnList = new List<Dictionary<string, string>>();

			string[] listofmembers;
			List<string> iListofMembers = new List<string>();

			char[] delimiterChars = { '[', ']', };
			listofmembers = stringFromServer.Split(delimiterChars);

			foreach (string row in listofmembers)
			{
				if (row.Length > 4)
					iListofMembers.Add(row);
			}

			string[] keys = iListofMembers[0].Split('"', '"');
			List<string> iKeys = new List<string>();
			foreach (string row in keys)
			{
				if (row != ",")
				{
					string final_row = JsonSerializer.DeserializeFromString<string>(row);   // This is to fix unicode characters
					iKeys.Add(final_row);
				}
			}
			iKeys.RemoveAt(0);
			iKeys.RemoveAt(iKeys.Count - 1);

			iListofMembers.RemoveAt(0);

			for (int i = 0; i < iListofMembers.Count; i++)
			{
				string[] values = iListofMembers[i].Split('"', '"');
				List<string> iValues = new List<string>();
				foreach (string row in values)
				{
					if (row != ",")
					{
						if (row.Contains("null"))
						{
							string[] rows = row.Split(',');
							foreach (string one in rows)
							{
								if (one == "null")
								{
									string final_row = JsonSerializer.DeserializeFromString<string>(one);   // This is to fix unicode characters
									iValues.Add(final_row);
								}
							}
						}
						else
						{
							string final_row = JsonSerializer.DeserializeFromString<string>(row);   // This is to fix unicode characters
							iValues.Add(final_row);
						}
					}
				}
				iValues.RemoveAt(0);
				iValues.RemoveAt(iValues.Count - 1);

				Dictionary<string, string> contact = new Dictionary<string, string>();
				for (int j = 0; j < iKeys.Count(); j++)
				{
					contact.Add(iKeys[j], iValues[j]);
				}
				returnList.Add(contact);
			}
			return returnList;
		}

		#endregion

		#region Generic API calling method

		/// <summary>
		/// Generic Export API call.  Expects to be able to serialize the results
		/// to the specified type
		/// </summary>
		/// <param name="apiAction">The API action.  Example: helper/account-details</param>
		/// <param name="args">The object that will be serialized as parameters to the API call</param>
		/// <returns></returns>
		private List<Dictionary<string, string>> MakeExportAPICall(string apiAction, object args)
		{
			//  First, make sure the datacenter prefix is set properly.  
			//  If it's not, throw an exception:
			if (string.IsNullOrEmpty(_dataCenterPrefix))
				throw new ApplicationException("API key not valid (datacenter not specified)");

			//  Next, construct the full url based on the passed apiAction:
			string fullUrl = string.Format(_httpsUrl, _dataCenterPrefix, apiAction);

			//  Initialize the results to return:
			List<Dictionary<string, string>> returnList = new List<Dictionary<string, string>>();

			try
			{
				//  Call the API with the passed arguments:
				fullUrl = ReturnFullUrlWithParameters(fullUrl, args);
				string result = WebRequestExtensions.GetStringFromUrl(fullUrl);
				returnList = ParseExportApiResults(result);
				
			}
			catch (Exception ex)
			{
				string errorBody = ex.GetResponseBody();

				//  Serialize the error information:
				ApiError apiError = errorBody.FromJson<ApiError>();

				//  Throw a new exception based on this information:
				throw new MailChimpAPIException(apiError.Error, ex, apiError);
			}

			//  Return the results
			return returnList;
		}

		#endregion

		#region API Methods

		/// <summary>
		/// Exports/dumps members of a list and all of their associated details. 
		/// This is a very similar to exporting via the web interface.
		/// </summary>
		/// <param name="listId">the list id to connect to (can be gathered using GetLists())</param>
		/// <param name="status">optional - the status to get members for - one of(subscribed, unsubscribed, cleaned)</param>
		/// <param name="segment">refine the members list by segments (maximum of 5 conditions)</param>
		/// <param name="since">only return member whose data has changed since a GMT timestamp – in YYYY-MM-DD HH:mm:ss format</param>
		/// <param name="hashed"> if, instead of full list data, you'd prefer a hashed list of email addresses, set this to the hashing algorithm you expect. Currently only "sha256" is supported. NOT IN USE NOW</param>        
		/// <returns></returns>
		public List<Dictionary<string, string>> GetAllMembersForList(string listId, string status = "subscribed", CampaignSegmentOptions segment = null, string since = "1900-01-01 00:00:00", string hashed = "")
		//int start = 0, int limit = 25, string sort_field = "", string sort_dir = "ASC", CampaignSegmentOptions segment = null)
		{
			//  Our api action:
			string apiAction = "list";

			//  Create our arguments object:
			object args = new
			{
				apikey = this.APIKey,
				id = listId,
				status = status,
				//segment = segment,
				since = since,
				//hashed = hashed
			};

			//  Make the call:
			return MakeExportAPICall(apiAction, args);
		}

		#endregion

	}
}