﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using sc2dsstats.rest.Exceptions;
using System.Threading.Tasks;

namespace sc2dsstats.rest.Middleware
{
    public class HttpExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public HttpExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next.Invoke(context);
            }
            catch (HttpException httpException)
            {
                context.Response.StatusCode = httpException.StatusCode;
                var feature = context.Features.Get<IHttpResponseFeature>();
                feature.ReasonPhrase = httpException.Message;
            }
        }
    }
}
