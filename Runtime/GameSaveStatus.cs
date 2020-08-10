// Copyright (c) John David Bonifacio Uy
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//Values taken from https://docs.microsoft.com/en-us/uwp/api/windows.gaming.xboxlive.storage.gamesaveerrorstatus?view=winrt-19041
namespace Microsoft.Xbox.Services.ConnectedStorage
{
    public enum GameSaveStatus
    {
        Abort                   = -2147467260,
        BlobNotFound            = -2138898424,
        ContainerNotInSync      = -2138898422,
        ContainerSyncFailed     = -2138898421,
        InvalidContainerName    = -2138898431,
        NoAccess                = -2138898430,
        NoXboxLiveInfo          = -2138898423,
        ObjectExpired           = -2138898419,
        Ok                      = 0,
        OutOfLocalStorage       = -2138898429,
        ProvidedBufferTooSmall  = -2138898425,
        QuotaExceeded           = -2138898426,
        UpdateTooBig            = -2138898427,
        UserCanceled            = -2138898428,
        UserHasNoXboxLiveInfo   = -2138898420,
    }
}

