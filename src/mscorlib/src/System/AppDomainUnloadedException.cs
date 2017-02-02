// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
** 
**
** Purpose: Exception class for attempt to access an unloaded AppDomain
**
**
=============================================================================*/

namespace System {

    using System.Runtime.Serialization;

    [System.Runtime.InteropServices.ComVisible(true)]
    [Serializable]
    internal class AppDomainUnloadedException : SystemException {
        public AppDomainUnloadedException() 
            : base(Environment.GetResourceString("Arg_AppDomainUnloadedException")) {
            SetErrorCode(__HResults.COR_E_APPDOMAINUNLOADED);
        }
    }
}

