using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Mvc;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Editors;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.Models.ContentEditing;
using Umbraco.Web.WebApi;
using Umbraco.Web.WebApi.Filters;

namespace Umbraco.Web.Editors
{
    /// <summary>
    /// An abstract base controller used for media/content (and probably members) to try to reduce code replication.
    /// </summary>
    [OutgoingDateTimeFormat]
    public abstract class ContentControllerBase : UmbracoAuthorizedJsonController
    {
        /// <summary>
        /// Constructor
        /// </summary>
        protected ContentControllerBase()
            : this(UmbracoContext.Current)
        {            
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="umbracoContext"></param>
        protected ContentControllerBase(UmbracoContext umbracoContext)
            : base(umbracoContext)
        {
        }

        protected HttpResponseMessage HandleContentNotFound(object id, bool throwException = true)
        {
            ModelState.AddModelError("id", string.Format("content with id: {0} was not found", id));
            var errorResponse = Request.CreateErrorResponse(
                HttpStatusCode.NotFound,
                ModelState);
            if (throwException)
            {
                throw new HttpResponseException(errorResponse);    
            }
            return errorResponse;
        }

        protected void UpdateName<TPersisted>(ContentBaseItemSave<TPersisted> contentItem) 
            where TPersisted : IContentBase
        {
            //Don't update the name if it is empty
            if (!contentItem.Name.IsNullOrWhiteSpace())
            {
                contentItem.PersistedContent.Name = contentItem.Name;
            }
        }

        protected HttpResponseMessage PerformSort(ContentSortOrder sorted)
        {
            if (sorted == null)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }

            //if there's nothing to sort just return ok
            if (sorted.IdSortOrder.Length == 0)
            {
                return Request.CreateResponse(HttpStatusCode.OK);
            }

            return null;
        }

        protected void MapPropertyValues<TPersisted>(ContentBaseItemSave<TPersisted> contentItem)
            where TPersisted : IContentBase
        {
            //Map the property values
            foreach (var p in contentItem.ContentDto.Properties)
            {
                //get the dbo property
                var dboProperty = contentItem.PersistedContent.Properties[p.Alias];

                //create the property data to send to the property editor
                var d = new Dictionary<string, object>();
                //add the files if any
                var files = contentItem.UploadedFiles.Where(x => x.PropertyId == p.Id).ToArray();
                if (files.Any())
                {
                    d.Add("files", files);
                }
                var data = new ContentPropertyData(p.Value, p.PreValues, d);

                //get the deserialized value from the property editor
                if (p.PropertyEditor == null)
                {
                    LogHelper.Warn<ContentController>("No property editor found for property " + p.Alias);
                }
                else
                {
                    var valueEditor = p.PropertyEditor.ValueEditor;
                    //don't persist any bound value if the editor is readonly
                    if (valueEditor.IsReadOnly == false)
                    {
                        dboProperty.Value = p.PropertyEditor.ValueEditor.FormatDataForPersistence(data, dboProperty.Value);    
                    }
                    
                }
            }
        }

        protected void HandleInvalidModelState<T, TPersisted>(ContentItemDisplayBase<T, TPersisted> display) 
            where TPersisted : IContentBase 
            where T : ContentPropertyBasic
        {
            //lasty, if it is not valid, add the modelstate to the outgoing object and throw a 403
            if (!ModelState.IsValid)
            {
                display.Errors = ModelState.ToErrorDictionary();
                throw new HttpResponseException(Request.CreateValidationErrorResponse(display));
            }
        }

        /// <summary>
        /// A helper method to attempt to get the instance from the request storage if it can be found there,
        /// otherwise gets it from the callback specified
        /// </summary>
        /// <typeparam name="TPersisted"></typeparam>
        /// <param name="getFromService"></param>
        /// <returns></returns>
        /// <remarks>
        /// This is useful for when filters have alraedy looked up a persisted entity and we don't want to have
        /// to look it up again.
        /// </remarks>
        protected TPersisted GetEntityFromRequest<TPersisted>(Func<TPersisted> getFromService)
            where TPersisted : IContentBase
        {
            return Request.Properties.ContainsKey(typeof (TPersisted).ToString()) == false
                       ? getFromService()
                       : (TPersisted) Request.Properties[typeof (TPersisted).ToString()];
        } 

    }
}