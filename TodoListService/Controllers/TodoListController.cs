//----------------------------------------------------------------------------------------------
//    Copyright 2014 Microsoft Corporation
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//----------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

// The following using statements were added for this sample.
using System.Collections.Concurrent;
using TodoListService.Models;
using System.Security.Claims;

namespace TodoListService.Controllers
{
    [Authorize]
    public class TodoListController : ApiController
    {
        //
        // To Do items list for all users.  Since the list is stored in memory, it will go away if the service is cycled.
        //
        static ConcurrentBag<TodoItem> todoBag = new ConcurrentBag<TodoItem>();

        // GET api/todolist
        public IEnumerable<TodoItem> Get()
        {
            //
            // The Scope claim tells you what permissions the client application has in the service.
            // In this case we look for a scope value of user_impersonation, or full access to the service as the user.
            //
            if (ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/scope").Value != "user_impersonation")
            {
                throw new HttpResponseException(new HttpResponseMessage { StatusCode = HttpStatusCode.Unauthorized, ReasonPhrase = "The Scope claim does not contain 'user_impersonation' or scope claim not found" });
            }

            // A user's To Do list is keyed off of the NameIdentifier claim, which contains an immutable, unique identifier for the user.
            Claim subject = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier);

            return from todo in todoBag
                   where todo.Owner == subject.Value
                   select todo;
        }

        // POST api/todolist
        public void Post(TodoItem todo)
        {
            if (ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/scope").Value != "user_impersonation")
            {
                throw new HttpResponseException(new HttpResponseMessage { StatusCode = HttpStatusCode.Unauthorized, ReasonPhrase = "The Scope claim does not contain 'user_impersonation' or scope claim not found" });
            }

			//if (ClaimsPrincipal.Current.FindFirst(ClaimTypes.GroupSid).Value == "8f27c022-7ee8-4de7-aa64-89173fb17b1b")
			//{
			//}

			//var groups = ClaimsPrincipal.Current.FindAll(ClaimTypes.GroupSid).Select(c => c.Value).ToArray();

			var principal = ClaimsPrincipal.Current;
			//Claim writeValuesClaim = principal.Claims.FirstOrDefault(
			//	c => c.Type == "http://schemas.microsoft.com/identity/claims/scope" &&
			//		c.Value.Contains("Read_Write_CloudAlloc_WebAPI"));

			// Look for the groups claim for the 'Dev/Test' group.
			const string devTestGroup = "8f27c022-7ee8-4de7-aa64-89173fb17b1b";
			Claim groupDevTestClaim = principal.Claims.FirstOrDefault(
				c => c.Type == "groups" &&
					c.Value.Equals(devTestGroup, StringComparison.CurrentCultureIgnoreCase));

			// If the app has write permissions and the user is in the Dev/Test group...
			//if ((null != writeValuesClaim) && (null != groupDevTestClaim))
			//{
			//	//
			//	// Code to add the resource goes here.
			//	//
			//	return Request.CreateResponse(HttpStatusCode.Created);
			//}


			if (null != todo && !string.IsNullOrWhiteSpace(todo.Title))
            {
                todoBag.Add(new TodoItem { Title = todo.Title, Owner = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier).Value });
            }
        }
    }
}
