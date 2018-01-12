//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.SqlTools.Dmp.Contracts;
using Microsoft.SqlTools.ServiceLayer.Workspace.Contracts;

namespace Microsoft.SqlTools.ServiceLayer.LanguageServices.Contracts
{
    public static class DefinitionRequest
    {
        public static readonly
            RequestType<TextDocumentPosition, Location[]> Type =
            RequestType<TextDocumentPosition, Location[]>.Create("textDocument/definition");
    }
}

