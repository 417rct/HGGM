﻿using System;
using HGGM.Services.Authorization.Simple;
using Microsoft.AspNetCore.Authorization;

namespace HGGM.Services.Authorization
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class PermissionAttribute : AuthorizeAttribute
    {
        public PermissionAttribute(SimplePermissionType permission) : base($"Permission{permission}")
        {
        }
    }
}