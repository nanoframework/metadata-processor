//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

#include <nanoHAL_v2.h>
#include <nanoHAL_Network.h>

uint32_t FindNextBlock(uint32_t startAddress, uint32_t endAddress, const unsigned char *marker)
{
    // all configuration markers are 4 bytes length

    unsigned char *cursor = (unsigned char *)startAddress;

    while (cursor < (unsigned char *)endAddress - 4)
    {
        if (memcmp(cursor, marker, 4) == 0)
        {
            // found one!
            break;
        }

        cursor++;
    }

    return (uint32_t)cursor;
}

uint32_t GetBlockCount(uint32_t startAddress, uint32_t endAddress, uint32_t blockSize, const unsigned char *marker)
{
    // all configuration markers are 4 bytes length
    int blockCount = 0;

    unsigned char *cursor = (unsigned char *)startAddress;

    while (cursor < (unsigned char *)endAddress - 4)
    {
        if (memcmp(cursor, marker, 4) == 0)
        {
            // found one!
            blockCount++;

            // bump cursor to the end of the config block size
            cursor += blockSize;
        }
        else
        {
            cursor++;
        }
    }

    return blockCount;
}

__nfweak void *ConfigurationManager_FindNetworkConfigurationBlocks(uint32_t startAddress, uint32_t endAddress)
{
    uint32_t nextBlock = startAddress;

    // first pass: find out how many blocks of this type we have
    uint32_t blockCount = GetBlockCount(
        startAddress,
        endAddress,
        sizeof(HAL_Configuration_NetworkInterface),
        c_MARKER_CONFIGURATION_NETWORK_V1);

    // allocate config struct
    HAL_CONFIGURATION_NETWORK *networkConfigs = (HAL_CONFIGURATION_NETWORK *)platform_malloc(
        offsetof(HAL_CONFIGURATION_NETWORK, Configs) + blockCount * sizeof(networkConfigs->Configs[0]));

    // set collection count
    networkConfigs->Count = blockCount;

    if (blockCount > 0)
    {

        // second pass: get address of each config block
        for (uint32_t i = 0; i < blockCount; i++)
        {
            nextBlock = FindNextBlock(nextBlock, endAddress, c_MARKER_CONFIGURATION_NETWORK_V1);
            networkConfigs->Configs[i] = (HAL_Configuration_NetworkInterface *)nextBlock;
        }
    }

    return networkConfigs;
}

__nfweak void *ConfigurationManager_FindNetworkWireless80211ConfigurationBlocks(
    uint32_t startAddress,
    uint32_t endAddress)
{
    uint32_t nextBlock = startAddress;

    // first pass: find out how many blocks of this type we have
    uint32_t blockCount = GetBlockCount(
        startAddress,
        endAddress,
        sizeof(HAL_Configuration_Wireless80211),
        c_MARKER_CONFIGURATION_WIRELESS80211_V1);

    // allocate config struct
    HAL_CONFIGURATION_NETWORK_WIRELESS80211 *networkWirelessConfigs =
        (HAL_CONFIGURATION_NETWORK_WIRELESS80211 *)platform_malloc(
            offsetof(HAL_CONFIGURATION_NETWORK_WIRELESS80211, Configs) +
            blockCount * sizeof(networkWirelessConfigs->Configs[0]));

    // set collection count
    networkWirelessConfigs->Count = blockCount;

    if (blockCount > 0)
    {
        // second pass: get address of each config block
        for (uint32_t i = 0; i < blockCount; i++)
        {
            nextBlock = FindNextBlock(nextBlock, endAddress, c_MARKER_CONFIGURATION_WIRELESS80211_V1);
            networkWirelessConfigs->Configs[i] = (HAL_Configuration_Wireless80211 *)nextBlock;
        }
    }

    return networkWirelessConfigs;
}

__nfweak void *ConfigurationManager_FindX509CertificateConfigurationBlocks(uint32_t startAddress, uint32_t endAddress)
{
    uint32_t nextBlock = startAddress;
    uint32_t allocationSize = 0;

    // first pass: find out how many blocks of this type we have
    // because these blocks have an unknow size, need to call this without a fixed size
    uint32_t blockCount = GetBlockCount(startAddress, endAddress, 1, c_MARKER_CONFIGURATION_X509CAROOTBUNDLE_V1);

    // start computing allocation size, first part is the struct initial fields
    allocationSize = offsetof(HAL_CONFIGURATION_X509_CERTIFICATE, Certificates);

    // second pass: find out the size of each X509 certificate (because they can have different sizes and we need this
    // to allocate memory for the struct)
    if (blockCount > 0)
    {
        for (uint32_t i = 0; i < blockCount; i++)
        {
            nextBlock = FindNextBlock(nextBlock, endAddress, c_MARKER_CONFIGURATION_X509CAROOTBUNDLE_V1);

            // header
            allocationSize += offsetof(HAL_Configuration_X509CaRootBundle, Certificate);

            // certificate
            allocationSize += ((HAL_Configuration_X509CaRootBundle *)nextBlock)->CertificateSize;
        }
    }

    // allocate config struct
    HAL_CONFIGURATION_X509_CERTIFICATE *certificateStore =
        (HAL_CONFIGURATION_X509_CERTIFICATE *)platform_malloc(allocationSize);

    // set collection count
    certificateStore->Count = blockCount;

    if (blockCount > 0)
    {
        // second pass: get address of each config block
        for (uint32_t i = 0; i < blockCount; i++)
        {
            nextBlock = FindNextBlock(nextBlock, endAddress, c_MARKER_CONFIGURATION_X509CAROOTBUNDLE_V1);
            certificateStore->Certificates[i] = (HAL_Configuration_X509CaRootBundle *)nextBlock;
        }
    }

    return certificateStore;
}

__nfweak void *ConfigurationManager_FindX509DeviceCertificatesConfigurationBlocks(
    uint32_t startAddress,
    uint32_t endAddress)
{
    uint32_t nextBlock = startAddress;
    uint32_t allocationSize = 0;

    // first pass: find out how many blocks of this type we have
    // because these blocks have an unknow size, need to call this without a fixed size
    uint32_t blockCount = GetBlockCount(startAddress, endAddress, 1, c_MARKER_CONFIGURATION_X509DEVICECERTIFICATE_V1);

    // start computing allocation size, first part is the struct initial fields
    allocationSize = offsetof(HAL_CONFIGURATION_X509_DEVICE_CERTIFICATE, Certificates);

    // second pass: find out the size of each X509 certificate (because they can have different sizes and we need this
    // to allocate memory for the struct)
    if (blockCount > 0)
    {
        for (uint32_t i = 0; i < blockCount; i++)
        {
            nextBlock = FindNextBlock(nextBlock, endAddress, c_MARKER_CONFIGURATION_X509DEVICECERTIFICATE_V1);

            // header
            allocationSize += offsetof(HAL_Configuration_X509DeviceCertificate, Certificate);

            // certificate
            allocationSize += ((HAL_Configuration_X509DeviceCertificate *)nextBlock)->CertificateSize;
        }
    }

    // allocate config struct
    HAL_CONFIGURATION_X509_DEVICE_CERTIFICATE *deviceCertificates =
        (HAL_CONFIGURATION_X509_DEVICE_CERTIFICATE *)platform_malloc(allocationSize);

    // set collection count
    deviceCertificates->Count = blockCount;

    if (blockCount > 0)
    {
        // second pass: get address of each config block
        for (uint32_t i = 0; i < blockCount; i++)
        {
            nextBlock = FindNextBlock(nextBlock, endAddress, c_MARKER_CONFIGURATION_X509DEVICECERTIFICATE_V1);
            deviceCertificates->Certificates[i] = (HAL_Configuration_X509DeviceCertificate *)nextBlock;
        }
    }

    return deviceCertificates;
}

__nfweak HAL_Configuration_Wireless80211 *ConfigurationManager_GetWirelessConfigurationFromId(uint32_t configurationId)
{
    for (int i = 0; i < g_TargetConfiguration.Wireless80211Configs->Count; i++)
    {
        if (g_TargetConfiguration.Wireless80211Configs->Configs[i]->Id == configurationId)
        {
            // need to make a copy
            HAL_Configuration_Wireless80211 *configBlock =
                (HAL_Configuration_Wireless80211 *)platform_malloc(sizeof(HAL_Configuration_Wireless80211));

            // check allocation
            if (configBlock)
            {
                memcpy(
                    configBlock,
                    g_TargetConfiguration.Wireless80211Configs->Configs[i],
                    sizeof(HAL_Configuration_Wireless80211));

                return configBlock;
            }
        }
    }

    // not found
    return NULL;
}

__nfweak HAL_Configuration_WirelessAP *ConfigurationManager_GetWirelessAPConfigurationFromId(uint32_t configurationId)
{
    for (int i = 0; i < g_TargetConfiguration.WirelessAPConfigs->Count; i++)
    {
        if (g_TargetConfiguration.WirelessAPConfigs->Configs[i]->Id == configurationId)
        {
            // need to make a copy
            HAL_Configuration_WirelessAP *configBlock =
                (HAL_Configuration_WirelessAP *)platform_malloc(sizeof(HAL_Configuration_WirelessAP));

            // check allocation
            if (configBlock)
            {
                memcpy(
                    configBlock,
                    g_TargetConfiguration.WirelessAPConfigs->Configs[i],
                    sizeof(HAL_Configuration_WirelessAP));

                return configBlock;
            }
        }
    }

    // not found
    return NULL;
}

__nfweak HAL_Configuration_X509CaRootBundle *ConfigurationManager_GetCertificateStore()
{
    if (g_TargetConfiguration.CertificateStore->Count)
    {
        // need to make a copy
        // need to compute size as the cert size is variable
        int32_t blockSize = offsetof(HAL_Configuration_X509CaRootBundle, Certificate) +
                            g_TargetConfiguration.CertificateStore->Certificates[0]->CertificateSize;

        HAL_Configuration_X509CaRootBundle *configBlock =
            (HAL_Configuration_X509CaRootBundle *)platform_malloc(blockSize);

        // check allocation
        if (configBlock)
        {
            memcpy(configBlock, g_TargetConfiguration.CertificateStore->Certificates[0], blockSize);

            return configBlock;
        }
    }

    // not found
    return NULL;
}

__nfweak HAL_Configuration_X509DeviceCertificate *ConfigurationManager_GetDeviceCertificate()
{
    if (g_TargetConfiguration.DeviceCertificates->Count)
    {
        // need to make a copy
        // need to compute size as the cert size is variable
        int32_t blockSize = offsetof(HAL_Configuration_X509DeviceCertificate, Certificate) +
                            g_TargetConfiguration.DeviceCertificates->Certificates[0]->CertificateSize;

        HAL_Configuration_X509DeviceCertificate *configBlock =
            (HAL_Configuration_X509DeviceCertificate *)platform_malloc(blockSize);

        // check allocation
        if (configBlock)
        {
            memcpy(configBlock, g_TargetConfiguration.DeviceCertificates->Certificates[0], blockSize);

            return configBlock;
        }
    }

    // not found
    return NULL;
}

__nfweak bool ConfigurationManager_CheckExistingConfigurationBlock(
    void *existingConfigBlock,
    void *newConfigBlock,
    uint32_t existingConfigBlockSize,
    uint32_t newConfigBlockSize)
{
    // config blocks parameters are addresses
    uint8_t *cursor1 = (uint8_t *)existingConfigBlock;
    uint8_t *cursor2 = (uint8_t *)newConfigBlock;

    // obvious check
    if (existingConfigBlockSize != newConfigBlockSize)
    {
        return false;
    }

    return memcmp(cursor1, cursor2, existingConfigBlockSize) == 0;
}

__nfweak void ConfigurationManager_GetOemModelSku(char *model, size_t modelSkuSize)
{
    // default implementation
    // this is weak so the target can provide the implementation
    memset(model, 0, modelSkuSize);
}

__nfweak void ConfigurationManager_GetModuleSerialNumber(char *serialNumber, size_t serialNumberSize)
{
    // default implementation
    // this is weak so a manufacturer can provide a strong implementation
    memset(serialNumber, 0, serialNumberSize);
}
