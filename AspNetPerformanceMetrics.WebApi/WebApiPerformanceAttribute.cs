﻿using System.Linq;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace AspNetPerformance
{
    /// <summary>
    /// Action Filter used to track performance on WebAPI applications
    /// </summary>
    public class WebApiPerformanceAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// Constant to identify WebAPI Action Types (used in the instane name)
        /// </summary>
        public const string ACTION_TYPE = "API";

        /// <summary>
        /// Method that runs before the Web API action is invoked
        /// </summary>
        /// <param name="actionContext">An HttpActionContext with info about the action that is executing</param>
        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            // First thing is to check if performance is enabled globally
            if (ConfigInfo.Value.PerformanceEnabled == false)
            {
                return;
            }

            // Second thing is to check if performance tracking has been turned off for this action
            // If the DoNotTrackAttribute is present, then we set a flag not to track performance and return
            HttpActionDescriptor actionDescriptor = actionContext.ActionDescriptor;
            if (actionDescriptor.GetCustomAttributes<DoNotTrackPerformanceAttribute>().Count > 0
                || actionDescriptor.ControllerDescriptor.GetCustomAttributes<DoNotTrackPerformanceAttribute>().Count > 0)
            {
                return;
            }

            // ActionInfo encapsulates all the info about the action being invoked
            ActionInfo info = CreateActionInfo(actionContext);

            // PerformanceTracker is the object that tracks performance and is attached to the request
            PerformanceTracker tracker = new PerformanceTracker(info);

            // Store this on the request
            actionContext.Request.Properties.Add(GetType().FullName, tracker);

            // Process the action start - this is what starts the timer and increments any
            // required counters before the action executes
            tracker.ProcessActionStart();
        }

        /// <summary>
        /// Method that executes after the WebAPI action has completed
        /// </summary>
        /// <param name="actionExecutedContext">An HttpActionExecutedContext object with info about the action that just executed</param>
        public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
        {
            string key = GetType().FullName;

            // Check the request properties for the PerformanceTracker object.  If it does not exist
            // then performance isn't being tracked on this action, so just return
            if (actionExecutedContext.Request.Properties.ContainsKey(key) == false)
            {
                return;
            }

            // Get the PerformanceTrcker object
            // Make sure the object isn't null (failed cast).  ProcessActionComplete stops the stopwach
            // and updates the performance counters
            if (actionExecutedContext.Request.Properties[key] is PerformanceTracker tracker)
            {
                bool exceptionThrown = (actionExecutedContext.Exception != null);
                tracker.ProcessActionComplete(exceptionThrown);
            }
        }

        /// <summary>
        /// Helper method to create the ActionInfo object containing the info about the action that is getting called
        /// </summary>
        /// <param name="actionContext">The HttpActionContext from the OnActionExecuting() method</param>
        /// <returns></returns>
        private ActionInfo CreateActionInfo(HttpActionContext actionContext)
        {
            var parameters = actionContext.ActionDescriptor.GetParameters().Select(p => p.ParameterName);
            string parameterString = string.Join(",", parameters);

            int processId = ConfigInfo.Value.ProcessId;
            string controllerName = actionContext.ControllerContext.ControllerDescriptor.ControllerName;
            string actionName = actionContext.ActionDescriptor.ActionName;
            string httpMethod = HttpContext.Current.Request.HttpMethod;
            int contentLength = HttpContext.Current.Request.ContentLength;

            ActionInfo info = new ActionInfo(processId, ACTION_TYPE,
                controllerName, actionName, httpMethod, parameterString, contentLength);

            return info;
        }
    }
}
