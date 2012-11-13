﻿#region [ License information          ]
/* ************************************************************
 * 
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
 *    
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *    
 *        http://www.apache.org/licenses/LICENSE-2.0
 *    
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *    
 * ************************************************************/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using Couchbase;
using Couchbase.Configuration;
using Couchbase.Management;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Results;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Enyim.Caching.Memcached.Results.Extensions;
using Newtonsoft.Json.Serialization;
using System.Data.Entity.Design.PluralizationServices;
using System.Globalization;

namespace CouchbaseBeersWeb.Models
{
	public abstract class WebRepositoryBase<T> where T : ModelBase
	{
		private const string ITEMS_CLIENT_KEY = "CouchbaseClient";
		private const string ITEMS_CLUSTER_KEY = "CouchbaseCluster";
		private IList<string> _existingAllItemViews = new List<string>();

		protected CouchbaseClient _Client
		{
			get { return getCouchbaseClient(); }
		}

		public IStoreOperationResult Create(string key, T value)
		{			
			return _Client.ExecuteStore(StoreMode.Add, key, serializeAndIgnoreId(value));
		}

		public IStoreOperationResult Update(string key, T value)
		{
			return _Client.ExecuteStore(StoreMode.Replace, key, serializeAndIgnoreId(value));
		}

		public IStoreOperationResult Save(string key, T value)
		{
			return _Client.ExecuteStore(StoreMode.Set, key, serializeAndIgnoreId(value));
		}

		public IGetOperationResult<T> Get(string key)
		{
			var jsonResult =_Client.ExecuteGet<string>(key);
			var retval = new GetOperationResult<T>();
			jsonResult.Combine(retval);

			if (jsonResult.HasValue)
			{				
				retval.Value = JsonConvert.DeserializeObject<T>(jsonResult.Value);
				retval.Value.Id = key; //_id is not stored in document, generic view merges it into JSON on return
			}

			return retval;
		}

		public IEnumerable<T> GetAll(string startKey = null, string endKey = null, int limit = 50)
		{
			var viewName = PluralizationService.CreateService(CultureInfo.CurrentCulture).Pluralize(typeof(T).Name.ToLower());
			var view = _Client.GetView<T>(viewName, "all", true).Limit(limit);
			if (startKey != null) view.StartKey(startKey);
			if (endKey != null) view.EndKey(endKey);

			return view;
		}

		public IRemoveOperationResult Remove(string key)
		{
			return _Client.ExecuteRemove(key);
		}

		private CouchbaseClient getCouchbaseClient()
		{
			var ctx = HttpContext.Current.ApplicationInstance.Context;

			if (!ctx.Items.Contains(ITEMS_CLIENT_KEY))
			{
				ctx.Items[ITEMS_CLIENT_KEY] = new CouchbaseClient();
			}

			return ctx.Items[ITEMS_CLIENT_KEY] as CouchbaseClient;
		}

		private string serializeAndIgnoreId(T obj)
		{
			var json = JsonConvert.SerializeObject(obj, 
				new JsonSerializerSettings()
				{
					ContractResolver = new DocumentIdContractResolver()
				});

			return json;
		}

		private class DocumentIdContractResolver : DefaultContractResolver
		{
			protected override List<System.Reflection.MemberInfo>
			GetSerializableMembers(Type objectType)
			{
				return base.GetSerializableMembers(objectType).Where(o => o.Name != "Id").ToList();
			}
		}

	}
}