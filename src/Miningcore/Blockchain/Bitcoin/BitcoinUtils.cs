using System;
using System.Diagnostics;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Miningcore.Blockchain.Bitcoin
{
    public static class BitcoinUtils
    {
        /// <summary>
        /// Converts a Base58Check-encoded P2PKH or P2SH address to an IDestination.
        /// </summary>
        public static IDestination AddressToDestination(string address, Network expectedNetwork)
        {
            var decoded = Encoders.Base58Check.DecodeData(address);
            var networkVersionBytes = expectedNetwork.GetVersionBytes(Base58Type.PUBKEY_ADDRESS, true);
            decoded = decoded.Skip(networkVersionBytes.Length).ToArray();
            var result = new KeyId(decoded);

            return result;
        }

        /// <summary>
        /// Converts a Bech32 SegWit address to an IDestination.
        /// </summary>
        public static IDestination BechSegwitAddressToDestination(string address, Network expectedNetwork)
        {
            var encoder = expectedNetwork.GetBech32Encoder(Bech32Type.WITNESS_PUBKEY_ADDRESS, true);
            var decoded = encoder.Decode(address, out var witVersion);
            var result = new WitKeyId(decoded);

            Debug.Assert(result.GetAddress(expectedNetwork).ToString() == address);
            return result;
        }

        /// <summary>
        /// Converts a Bitcoin Cash address (CashAddr or legacy) to an IDestination.
        /// Supports missing CashAddr prefixes by prepending them based on the network.
        /// </summary>
        public static IDestination BCashAddressToDestination(string address, Network expectedNetwork)
        {
            // Map the expectedNetwork's ChainName to BCash library's ChainName
            ChainName chainName = expectedNetwork.ChainName.ToString().ToLower() switch
            {
                "mainnet" or "main" => ChainName.Mainnet,
                "testnet4" or "test4" => ChainName.Testnet,
                "regtest" or "reg" => ChainName.Regtest,
                _ => throw new ArgumentException("Unknown network chain name", nameof(expectedNetwork))
            };

            // Get the appropriate Bitcoin Cash network instance
            var bcashNetwork = NBitcoin.Altcoins.BCash.Instance.GetNetwork(chainName);

            // If the address doesn't contain a colon, assume it's missing the CashAddr prefix or is legacy
            if (!address.Contains(":"))
            {
                // Check if it’s a legacy address (starts with '1' or '3')
                if (address.StartsWith("1") || address.StartsWith("3"))
                {
                    // Parse legacy address directly
                    var legacyAddress = bcashNetwork.Parse<BitcoinPubKeyAddress>(address);
                    return legacyAddress.ScriptPubKey.GetDestinationAddress(bcashNetwork);
                }

                // Otherwise, assume it’s a CashAddr without prefix and prepend the appropriate one
                if (chainName == ChainName.Mainnet)
                    address = "bitcoincash:" + address;
                else if (chainName == ChainName.Testnet)
                    address = "bchtest:" + address;
                else if (chainName == ChainName.Regtest)
                    address = "bchreg:" + address;
            }

            // Parse the address as a BCH CashAddr or legacy address
            var pubKeyAddress = bcashNetwork.Parse<NBitcoin.Altcoins.BCash.BTrashPubKeyAddress>(address);
            return pubKeyAddress.ScriptPubKey.GetDestinationAddress(bcashNetwork);
        }
    }
}