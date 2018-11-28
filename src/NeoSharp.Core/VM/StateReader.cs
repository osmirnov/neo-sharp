﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NeoSharp.Core.Blockchain.Repositories;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Extensions;
using NeoSharp.Core.Models;
using NeoSharp.Core.Network;
using NeoSharp.Core.SmartContract;
using NeoSharp.Types;
using NeoSharp.VM;

namespace NeoSharp.Core.VM
{
    public class StateReader : IDisposable
    {
        // TODO: Move const into app engine
        private const int EngineMaxArraySize = 1024;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IBlockRepository _blockRepository;
        private readonly ITransactionRepository _transactionRepository;

        private readonly ETriggerType _trigger;
        //public static event EventHandler<NotifyEventArgs> Notify;
        //public static event EventHandler<LogEventArgs> Log;

        //private readonly List<NotifyEventArgs> _notifications = new List<NotifyEventArgs>();
        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        //public IReadOnlyList<NotifyEventArgs> Notifications => _notifications;

        protected DataCache<UInt160, Account> Accounts { get; }

        protected DataCache<UInt256, Asset> Assets { get; }

        protected DataCache<UInt160, Contract> Contracts { get; }

        protected DataCache<StorageKey, StorageValue> Storages { get; }

        public StateReader(
            DataCache<UInt160, Account> accounts,
            DataCache<UInt256, Asset> assets,
            DataCache<UInt160, Contract> contracts,
            DataCache<StorageKey, StorageValue> storages,
            IInteropService interopService,
            IBlockchainContext blockchainContext,
            IBlockRepository blockRepository,
            ITransactionRepository transactionRepository,
            ETriggerType trigger)
        {
            Accounts = accounts;
            Assets = assets;
            Contracts = contracts;
            Storages = storages;
            _blockchainContext = blockchainContext;
            _blockRepository = blockRepository;
            _transactionRepository = transactionRepository;
            _trigger = trigger;

            //Standard Library
            interopService.RegisterMethod("System.Runtime.GetTrigger", Runtime_GetTrigger);
            interopService.RegisterMethod("System.Runtime.CheckWitness", Runtime_CheckWitness);
            interopService.RegisterMethod("System.Runtime.Notify", Runtime_Notify);
            interopService.RegisterMethod("System.Runtime.Log", Runtime_Log);
            interopService.RegisterMethod("System.Runtime.GetTime", Runtime_GetTime);
            interopService.RegisterMethod("System.Blockchain.GetHeight", Blockchain_GetHeight);
            interopService.RegisterMethod("System.Blockchain.GetHeader", Blockchain_GetHeader);
            interopService.RegisterMethod("System.Blockchain.GetBlock", Blockchain_GetBlock);
            interopService.RegisterMethod("System.Blockchain.GetTransaction", Blockchain_GetTransaction);
            interopService.RegisterMethod("System.Blockchain.GetTransactionHeight", Blockchain_GetTransactionHeight);
            interopService.RegisterMethod("System.Blockchain.GetContract", Blockchain_GetContract);
            interopService.RegisterMethod("System.Header.GetIndex", Header_GetIndex);
            interopService.RegisterMethod("System.Header.GetHash", Header_GetHash);
            interopService.RegisterMethod("System.Header.GetPrevHash", Header_GetPrevHash);
            interopService.RegisterMethod("System.Header.GetTimestamp", Header_GetTimestamp);
            interopService.RegisterMethod("System.Block.GetTransactionCount", Block_GetTransactionCount);
            interopService.RegisterMethod("System.Block.GetTransactions", Block_GetTransactions);
            interopService.RegisterMethod("System.Block.GetTransaction", Block_GetTransaction);
            interopService.RegisterMethod("System.Transaction.GetHash", Transaction_GetHash);
            interopService.RegisterMethod("System.Storage.GetContext", Storage_GetContext);
            interopService.RegisterMethod("System.Storage.GetReadOnlyContext", Storage_GetReadOnlyContext);
            interopService.RegisterMethod("System.Storage.Get", Storage_Get);
            interopService.RegisterMethod("System.StorageContext.AsReadOnly", StorageContext_AsReadOnly);

            //Neo Specified
            interopService.RegisterMethod("Neo.Blockchain.GetAccount", Blockchain_GetAccount);
            interopService.RegisterMethod("Neo.Blockchain.GetValidators", Blockchain_GetValidators);
            interopService.RegisterMethod("Neo.Blockchain.GetAsset", Blockchain_GetAsset);
            interopService.RegisterMethod("Neo.Header.GetVersion", Header_GetVersion);
            interopService.RegisterMethod("Neo.Header.GetMerkleRoot", Header_GetMerkleRoot);
            interopService.RegisterMethod("Neo.Header.GetConsensusData", Header_GetConsensusData);
            interopService.RegisterMethod("Neo.Header.GetNextConsensus", Header_GetNextConsensus);
            interopService.RegisterMethod("Neo.Transaction.GetType", Transaction_GetType);
            interopService.RegisterMethod("Neo.Transaction.GetAttributes", Transaction_GetAttributes);
            interopService.RegisterMethod("Neo.Transaction.GetInputs", Transaction_GetInputs);
            interopService.RegisterMethod("Neo.Transaction.GetOutputs", Transaction_GetOutputs);
            interopService.RegisterMethod("Neo.Transaction.GetReferences", Transaction_GetReferences);
            interopService.RegisterMethod("Neo.Transaction.GetUnspentCoins", Transaction_GetUnspentCoins);
            interopService.RegisterMethod("Neo.InvocationTransaction.GetScript", InvocationTransaction_GetScript);
            interopService.RegisterMethod("Neo.Attribute.GetUsage", Attribute_GetUsage);
            interopService.RegisterMethod("Neo.Attribute.GetData", Attribute_GetData);
            interopService.RegisterMethod("Neo.Input.GetHash", Input_GetHash);
            interopService.RegisterMethod("Neo.Input.GetIndex", Input_GetIndex);
            interopService.RegisterMethod("Neo.Output.GetAssetId", Output_GetAssetId);
            interopService.RegisterMethod("Neo.Output.GetValue", Output_GetValue);
            interopService.RegisterMethod("Neo.Output.GetScriptHash", Output_GetScriptHash);
            interopService.RegisterMethod("Neo.Account.GetScriptHash", Account_GetScriptHash);
            interopService.RegisterMethod("Neo.Account.GetVotes", Account_GetVotes);
            interopService.RegisterMethod("Neo.Account.GetBalance", Account_GetBalance);
            interopService.RegisterMethod("Neo.Asset.GetAssetId", Asset_GetAssetId);
            interopService.RegisterMethod("Neo.Asset.GetAssetType", Asset_GetAssetType);
            interopService.RegisterMethod("Neo.Asset.GetAmount", Asset_GetAmount);
            interopService.RegisterMethod("Neo.Asset.GetAvailable", Asset_GetAvailable);
            interopService.RegisterMethod("Neo.Asset.GetPrecision", Asset_GetPrecision);
            interopService.RegisterMethod("Neo.Asset.GetOwner", Asset_GetOwner);
            interopService.RegisterMethod("Neo.Asset.GetAdmin", Asset_GetAdmin);
            interopService.RegisterMethod("Neo.Asset.GetIssuer", Asset_GetIssuer);
            interopService.RegisterMethod("Neo.Contract.GetScript", Contract_GetScript);
            interopService.RegisterMethod("Neo.Contract.IsPayable", Contract_IsPayable);
            interopService.RegisterMethod("Neo.Storage.Find", Storage_Find);
            // TODO: APIs for enumeration and iteration
            //interopService.RegisterMethod("Neo.Enumerator.Create", Enumerator_Create);
            //interopService.RegisterMethod("Neo.Enumerator.Next", Enumerator_Next);
            //interopService.RegisterMethod("Neo.Enumerator.Value", Enumerator_Value);
            //interopService.RegisterMethod("Neo.Enumerator.Concat", Enumerator_Concat);
            //interopService.RegisterMethod("Neo.Iterator.Create", Iterator_Create);
            //interopService.RegisterMethod("Neo.Iterator.Key", Iterator_Key);
            //interopService.RegisterMethod("Neo.Iterator.Keys", Iterator_Keys);
            //interopService.RegisterMethod("Neo.Iterator.Values", Iterator_Values);

            #region Aliases
            //interopService.RegisterMethod("Neo.Iterator.Next", Enumerator_Next);
            //interopService.RegisterMethod("Neo.Iterator.Value", Enumerator_Value);
            #endregion

            #region Old APIs
            interopService.RegisterMethod("Neo.Runtime.GetTrigger", Runtime_GetTrigger);
            interopService.RegisterMethod("Neo.Runtime.CheckWitness", Runtime_CheckWitness);
            interopService.RegisterMethod("AntShares.Runtime.CheckWitness", Runtime_CheckWitness);
            interopService.RegisterMethod("Neo.Runtime.Notify", Runtime_Notify);
            interopService.RegisterMethod("AntShares.Runtime.Notify", Runtime_Notify);
            interopService.RegisterMethod("Neo.Runtime.Log", Runtime_Log);
            interopService.RegisterMethod("AntShares.Runtime.Log", Runtime_Log);
            interopService.RegisterMethod("Neo.Runtime.GetTime", Runtime_GetTime);
            interopService.RegisterMethod("Neo.Blockchain.GetHeight", Blockchain_GetHeight);
            interopService.RegisterMethod("AntShares.Blockchain.GetHeight", Blockchain_GetHeight);
            interopService.RegisterMethod("Neo.Blockchain.GetHeader", Blockchain_GetHeader);
            interopService.RegisterMethod("AntShares.Blockchain.GetHeader", Blockchain_GetHeader);
            interopService.RegisterMethod("Neo.Blockchain.GetBlock", Blockchain_GetBlock);
            interopService.RegisterMethod("AntShares.Blockchain.GetBlock", Blockchain_GetBlock);
            interopService.RegisterMethod("Neo.Blockchain.GetTransaction", Blockchain_GetTransaction);
            interopService.RegisterMethod("AntShares.Blockchain.GetTransaction", Blockchain_GetTransaction);
            interopService.RegisterMethod("Neo.Blockchain.GetTransactionHeight", Blockchain_GetTransactionHeight);
            interopService.RegisterMethod("AntShares.Blockchain.GetAccount", Blockchain_GetAccount);
            interopService.RegisterMethod("AntShares.Blockchain.GetValidators", Blockchain_GetValidators);
            interopService.RegisterMethod("AntShares.Blockchain.GetAsset", Blockchain_GetAsset);
            interopService.RegisterMethod("Neo.Blockchain.GetContract", Blockchain_GetContract);
            interopService.RegisterMethod("AntShares.Blockchain.GetContract", Blockchain_GetContract);
            interopService.RegisterMethod("Neo.Header.GetIndex", Header_GetIndex);
            interopService.RegisterMethod("Neo.Header.GetHash", Header_GetHash);
            interopService.RegisterMethod("AntShares.Header.GetHash", Header_GetHash);
            interopService.RegisterMethod("AntShares.Header.GetVersion", Header_GetVersion);
            interopService.RegisterMethod("Neo.Header.GetPrevHash", Header_GetPrevHash);
            interopService.RegisterMethod("AntShares.Header.GetPrevHash", Header_GetPrevHash);
            interopService.RegisterMethod("AntShares.Header.GetMerkleRoot", Header_GetMerkleRoot);
            interopService.RegisterMethod("Neo.Header.GetTimestamp", Header_GetTimestamp);
            interopService.RegisterMethod("AntShares.Header.GetTimestamp", Header_GetTimestamp);
            interopService.RegisterMethod("AntShares.Header.GetConsensusData", Header_GetConsensusData);
            interopService.RegisterMethod("AntShares.Header.GetNextConsensus", Header_GetNextConsensus);
            interopService.RegisterMethod("Neo.Block.GetTransactionCount", Block_GetTransactionCount);
            interopService.RegisterMethod("AntShares.Block.GetTransactionCount", Block_GetTransactionCount);
            interopService.RegisterMethod("Neo.Block.GetTransactions", Block_GetTransactions);
            interopService.RegisterMethod("AntShares.Block.GetTransactions", Block_GetTransactions);
            interopService.RegisterMethod("Neo.Block.GetTransaction", Block_GetTransaction);
            interopService.RegisterMethod("AntShares.Block.GetTransaction", Block_GetTransaction);
            interopService.RegisterMethod("Neo.Transaction.GetHash", Transaction_GetHash);
            interopService.RegisterMethod("AntShares.Transaction.GetHash", Transaction_GetHash);
            interopService.RegisterMethod("AntShares.Transaction.GetType", Transaction_GetType);
            interopService.RegisterMethod("AntShares.Transaction.GetAttributes", Transaction_GetAttributes);
            interopService.RegisterMethod("AntShares.Transaction.GetInputs", Transaction_GetInputs);
            interopService.RegisterMethod("AntShares.Transaction.GetOutputs", Transaction_GetOutputs);
            interopService.RegisterMethod("AntShares.Transaction.GetReferences", Transaction_GetReferences);
            interopService.RegisterMethod("AntShares.Attribute.GetUsage", Attribute_GetUsage);
            interopService.RegisterMethod("AntShares.Attribute.GetData", Attribute_GetData);
            interopService.RegisterMethod("AntShares.Input.GetHash", Input_GetHash);
            interopService.RegisterMethod("AntShares.Input.GetIndex", Input_GetIndex);
            interopService.RegisterMethod("AntShares.Output.GetAssetId", Output_GetAssetId);
            interopService.RegisterMethod("AntShares.Output.GetValue", Output_GetValue);
            interopService.RegisterMethod("AntShares.Output.GetScriptHash", Output_GetScriptHash);
            interopService.RegisterMethod("AntShares.Account.GetScriptHash", Account_GetScriptHash);
            interopService.RegisterMethod("AntShares.Account.GetVotes", Account_GetVotes);
            interopService.RegisterMethod("AntShares.Account.GetBalance", Account_GetBalance);
            interopService.RegisterMethod("AntShares.Asset.GetAssetId", Asset_GetAssetId);
            interopService.RegisterMethod("AntShares.Asset.GetAssetType", Asset_GetAssetType);
            interopService.RegisterMethod("AntShares.Asset.GetAmount", Asset_GetAmount);
            interopService.RegisterMethod("AntShares.Asset.GetAvailable", Asset_GetAvailable);
            interopService.RegisterMethod("AntShares.Asset.GetPrecision", Asset_GetPrecision);
            interopService.RegisterMethod("AntShares.Asset.GetOwner", Asset_GetOwner);
            interopService.RegisterMethod("AntShares.Asset.GetAdmin", Asset_GetAdmin);
            interopService.RegisterMethod("AntShares.Asset.GetIssuer", Asset_GetIssuer);
            interopService.RegisterMethod("AntShares.Contract.GetScript", Contract_GetScript);
            interopService.RegisterMethod("Neo.Storage.GetContext", Storage_GetContext);
            interopService.RegisterMethod("AntShares.Storage.GetContext", Storage_GetContext);
            interopService.RegisterMethod("Neo.Storage.GetReadOnlyContext", Storage_GetReadOnlyContext);
            interopService.RegisterMethod("Neo.Storage.Get", Storage_Get);
            interopService.RegisterMethod("AntShares.Storage.Get", Storage_Get);
            interopService.RegisterMethod("Neo.StorageContext.AsReadOnly", StorageContext_AsReadOnly);
            #endregion
        }

        internal bool CheckStorageContext(StorageContext context)
        {
            var contract = Contracts.TryGet(context.ScriptHash);

            return contract != null && contract.HasStorage;
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
                disposable.Dispose();
            _disposables.Clear();
        }

        protected virtual bool Runtime_GetTrigger(IStackAccessor stack)
        {
            stack.Push((int)_trigger);
            return true;
        }

        protected bool CheckWitness(IStackAccessor stack, UInt160 hash)
        {
            // TODO:
            //IVerifiable container = (IVerifiable)engine.ScriptContainer;
            //UInt160[] _hashes_for_verifying = container.GetScriptHashesForVerifying();
            //return _hashes_for_verifying.Contains(hash);
            return true;
        }

        protected bool CheckWitness(IStackAccessor stack, ECPoint pubkey)
        {
            // TODO:
            //return CheckWitness(engine, Contract.CreateSignatureRedeemScript(pubkey).ToScriptHash());
            return true;
        }

        protected virtual bool Runtime_CheckWitness(IStackAccessor stack)
        {
            var hashOrPubkey = stack.PopByteArray();
            // TODO:
            //bool result;
            //if (hashOrPubkey.Length == 20)
            //    result = CheckWitness(engine, new UInt160(hashOrPubkey));
            //else if (hashOrPubkey.Length == 33)
            //    result = CheckWitness(engine, new ECPoint(hashOrPubkey));
            //else
            //    return false;
            //stack.Push(result);
            return true;
        }

        protected virtual bool Runtime_Notify(IStackAccessor stack)
        {
            // TODO:
            //var state = stack.Pop();
            //var notification = new NotifyEventArgs(engine.ScriptContainer, new UInt160(engine.CurrentContext.ScriptHash), state);
            //Notify?.Invoke(this, notification);
            //notifications.Add(notification);
            return true;
        }

        protected virtual bool Runtime_Log(IStackAccessor stack)
        {
            // TODO:
            //var message = Encoding.UTF8.GetString(stack.PopByteArray());
            //Log?.Invoke(this, new LogEventArgs(engine.ScriptContainer, new UInt160(engine.CurrentContext.ScriptHash), message));
            return true;
        }

        protected virtual bool Runtime_GetTime(IStackAccessor stack)
        {
            stack.Push(_blockchainContext.LastBlockHeader.Timestamp + 15);
            return true;
        }

        protected virtual bool Blockchain_GetHeight(IStackAccessor stack)
        {
            stack.Push(_blockchainContext.CurrentBlock.Index);
            return true;
        }

        protected virtual bool Blockchain_GetHeader(IStackAccessor stack)
        {
            var data = stack.PopByteArray();

            BlockHeader blockHeader;

            if (data.Length <= 5)
            {
                var height = (uint)new BigInteger(data);

                blockHeader = _blockRepository.GetBlockHeader(height).Result;
            }
            else if (data.Length == 32)
            {
                var hash = new UInt256(data);

                blockHeader = _blockRepository.GetBlockHeader(hash).Result;
            }
            else
            {
                return false;
            }

            stack.Push(blockHeader);

            return true;
        }

        protected virtual bool Blockchain_GetBlock(IStackAccessor stack)
        {
            var data = stack.PopByteArray();

            Block block;

            if (data.Length <= 5)
            {
                var height = (uint)new BigInteger(data);

                block = _blockRepository.GetBlock(height).Result;
            }
            else if (data.Length == 32)
            {
                var hash = new UInt256(data);

                block = _blockRepository.GetBlock(hash).Result;
            }
            else
            {
                return false;
            }

            stack.Push(block);

            return true;
        }

        protected virtual bool Blockchain_GetTransaction(IStackAccessor stack)
        {
            var hash = stack.PopByteArray();
            var transaction = _transactionRepository.GetTransaction(new UInt256(hash)).Result;

            stack.Push(transaction);

            return true;
        }

        protected virtual bool Blockchain_GetTransactionHeight(IStackAccessor stack)
        {
            var hash = stack.PopByteArray();
            // TODO: looks like we need an index transaction in the block;
            var height = 0;
            // var transaction = _transactionRepository.GetTransaction(new UInt256(hash)).Result;

            stack.Push(height);

            return true;
        }

        protected virtual bool Blockchain_GetAccount(IStackAccessor stack)
        {
            var hash = new UInt160(stack.PopByteArray());
            var account = Accounts.GetOrAdd(hash, () => new Account(hash));
            stack.Push(account);
            return true;
        }

        protected virtual bool Blockchain_GetValidators(IStackAccessor stack)
        {
            // TODO: looks like we need to get all validators
            //ECPoint[] validators = _blockchain.GetValidators();
            //stack.Push(validators.Select(p => (StackItem)p.EncodePoint(true)).ToArray());
            return true;
        }

        protected virtual bool Blockchain_GetAsset(IStackAccessor stack)
        {
            var hash = new UInt256(stack.PopByteArray());
            var asset = Assets.TryGet(hash);
            if (asset == null) return false;
            stack.Push(asset);
            return true;
        }

        protected virtual bool Blockchain_GetContract(IStackAccessor stack)
        {
            var hash = new UInt160(stack.PopByteArray());
            var contract = Contracts.TryGet(hash);
            if (contract == null)
                stack.Push(Array.Empty<byte>());
            else
                stack.Push(contract);
            return true;
        }

        protected virtual bool Header_GetIndex(IStackAccessor stack)
        {
            var blockBase = stack.Pop<BlockBase>();
            if (blockBase == null) return false;

            stack.Push(blockBase.Index);

            return true;
        }

        protected virtual bool Header_GetHash(IStackAccessor stack)
        {
            var blockBase = stack.Pop<BlockBase>();
            if (blockBase == null) return false;

            stack.Push(blockBase.Hash.ToArray());

            return true;
        }

        protected virtual bool Header_GetVersion(IStackAccessor stack)
        {
            var blockBase = stack.Pop<BlockBase>();
            if (blockBase == null) return false;

            stack.Push(blockBase.Version);

            return true;
        }

        protected virtual bool Header_GetPrevHash(IStackAccessor stack)
        {
            var blockBase = stack.Pop<BlockBase>();
            if (blockBase == null) return false;

            stack.Push(blockBase.PreviousBlockHash.ToArray());

            return true;
        }

        protected virtual bool Header_GetMerkleRoot(IStackAccessor stack)
        {
            // TODO: Should MerkleRoot be in BlockBase?
            var blockBase = stack.Pop<BlockHeader>();
            if (blockBase == null) return false;

            stack.Push(blockBase.MerkleRoot.ToArray());

            return true;
        }

        protected virtual bool Header_GetTimestamp(IStackAccessor stack)
        {
            var blockBase = stack.Pop<BlockBase>();
            if (blockBase == null) return false;

            stack.Push(blockBase.Timestamp);

            return true;
        }

        protected virtual bool Header_GetConsensusData(IStackAccessor stack)
        {
            var blockBase = stack.Pop<BlockBase>();
            if (blockBase == null) return false;

            stack.Push(blockBase.ConsensusData);

            return true;
        }

        protected virtual bool Header_GetNextConsensus(IStackAccessor stack)
        {
            var blockBase = stack.Pop<BlockBase>();
            if (blockBase == null) return false;

            stack.Push(blockBase.NextConsensus.ToArray());

            return true;
        }

        protected virtual bool Block_GetTransactionCount(IStackAccessor stack)
        {
            var block = stack.Pop<Block>();
            if (block == null) return false;

            stack.Push(block.Transactions.Length);

            return true;
        }

        protected virtual bool Block_GetTransactions(IStackAccessor stack)
        {
            var block = stack.Pop<Block>();
            if (block == null) return false;
            if (block.Transactions.Length > EngineMaxArraySize) return false;

            stack.Push(block.Transactions);

            return true;
        }

        protected virtual bool Block_GetTransaction(IStackAccessor stack)
        {
            var block = stack.Pop<Block>();
            if (block == null) return false;

            var index = (int)stack.PopBigInteger();
            if (index < 0 || index >= block.Transactions.Length) return false;

            var transaction = block.Transactions[index];

            stack.Push(transaction);

            return true;
        }

        protected virtual bool Transaction_GetHash(IStackAccessor stack)
        {
            var transaction = stack.Pop<Transaction>();
            if (transaction == null) return false;

            stack.Push(transaction.Hash.ToArray());

            return true;
        }

        protected virtual bool Transaction_GetType(IStackAccessor stack)
        {
            var transaction = stack.Pop<Transaction>();
            if (transaction == null) return false;

            stack.Push((int)transaction.Type);

            return true;
        }

        protected virtual bool Transaction_GetAttributes(IStackAccessor stack)
        {
            var transaction = stack.Pop<Transaction>();
            if (transaction == null) return false;
            if (transaction.Attributes.Length > EngineMaxArraySize)
                return false;

            stack.Push(transaction.Attributes);

            return true;
        }

        protected virtual bool Transaction_GetInputs(IStackAccessor stack)
        {
            var transaction = stack.Pop<Transaction>();
            if (transaction == null) return false;
            if (transaction.Inputs.Length > EngineMaxArraySize)
                return false;

            stack.Push(transaction.Inputs);

            return true;
        }

        protected virtual bool Transaction_GetOutputs(IStackAccessor stack)
        {
            var transaction = stack.Pop<Transaction>();
            if (transaction == null) return false;
            if (transaction.Outputs.Length > EngineMaxArraySize)
                return false;

            stack.Push(transaction.Outputs);

            return true;
        }

        protected virtual bool Transaction_GetReferences(IStackAccessor stack)
        {
            var transaction = stack.Pop<Transaction>();
            if (transaction == null) return false;
            // TODO: Check refs length?
            if (transaction.Inputs.Length > EngineMaxArraySize)
                return false;

            var references = _transactionRepository.GetReferences(transaction).Result;
            stack.Push(transaction.Inputs.Select(i => references[i]).ToArray());

            return true;
        }

        protected virtual bool Transaction_GetUnspentCoins(IStackAccessor stack)
        {
            var transaction = stack.Pop<Transaction>();
            if (transaction == null) return false;

            var outputs = _transactionRepository.GetUnspent(transaction.Hash).Result;
            if (outputs.Count() > EngineMaxArraySize)
                return false;

            stack.Push(outputs.ToArray());

            return true;
        }

        protected virtual bool InvocationTransaction_GetScript(IStackAccessor stack)
        {
            var transaction = stack.Pop<InvocationTransaction>();
            if (transaction == null) return false;

            stack.Push(transaction.Script);

            return true;
        }

        protected virtual bool Attribute_GetUsage(IStackAccessor stack)
        {
            var transactionAttr = stack.Pop<TransactionAttribute>();
            if (transactionAttr == null) return false;

            stack.Push((int)transactionAttr.Usage);

            return true;
        }

        protected virtual bool Attribute_GetData(IStackAccessor stack)
        {
            var transactionAttr = stack.Pop<TransactionAttribute>();
            if (transactionAttr == null) return false;

            stack.Push(transactionAttr.Data);

            return true;
        }

        protected virtual bool Input_GetHash(IStackAccessor stack)
        {
            var coinReference = stack.Pop<CoinReference>();
            if (coinReference == null) return false;

            stack.Push(coinReference.PrevHash.ToArray());

            return true;
        }

        protected virtual bool Input_GetIndex(IStackAccessor stack)
        {
            var coinReference = stack.Pop<CoinReference>();
            if (coinReference == null) return false;

            stack.Push((int)coinReference.PrevIndex);

            return true;
        }

        protected virtual bool Output_GetAssetId(IStackAccessor stack)
        {
            var transactionOutput = stack.Pop<TransactionOutput>();
            if (transactionOutput == null) return false;

            stack.Push(transactionOutput.AssetId.ToArray());

            return true;
        }

        protected virtual bool Output_GetValue(IStackAccessor stack)
        {
            var transactionOutput = stack.Pop<TransactionOutput>();
            if (transactionOutput == null) return false;

            stack.Push(transactionOutput.Value.Value);

            return true;
        }

        protected virtual bool Output_GetScriptHash(IStackAccessor stack)
        {
            var transactionOutput = stack.Pop<TransactionOutput>();
            if (transactionOutput == null) return false;

            stack.Push(transactionOutput.ScriptHash.ToArray());

            return true;
        }

        protected virtual bool Account_GetScriptHash(IStackAccessor stack)
        {
            var account = stack.Pop<Account>();
            if (account == null) return false;

            stack.Push(account.ScriptHash.ToArray());

            return true;
        }

        protected virtual bool Account_GetVotes(IStackAccessor stack)
        {
            var account = stack.Pop<Account>();
            if (account == null) return false;

            // TODO: it was EncodePoint before. Check with NEO
            account.Votes.Select(v => v.EncodedData).ForEach(stack.Push);

            return true;
        }

        protected virtual bool Account_GetBalance(IStackAccessor stack)
        {
            var account = stack.Pop<Account>();
            if (account == null) return false;

            var assetId = new UInt256(stack.PopByteArray());
            var balance = account.Balances.TryGetValue(assetId, out var value) ? value : Fixed8.Zero;

            stack.Push(balance.Value);

            return true;
        }

        protected virtual bool Asset_GetAssetId(IStackAccessor stack)
        {
            var asset = stack.Pop<Asset>();
            if (asset == null) return false;

            stack.Push(asset.Id.ToArray());

            return true;
        }

        protected virtual bool Asset_GetAssetType(IStackAccessor stack)
        {
            var asset = stack.Pop<Asset>();
            if (asset == null) return false;

            stack.Push((int)asset.AssetType);

            return true;
        }

        protected virtual bool Asset_GetAmount(IStackAccessor stack)
        {
            var asset = stack.Pop<Asset>();
            if (asset == null) return false;

            stack.Push(asset.Amount.Value);

            return true;
        }

        protected virtual bool Asset_GetAvailable(IStackAccessor stack)
        {
            var asset = stack.Pop<Asset>();
            if (asset == null) return false;

            stack.Push(asset.Available.Value);

            return true;
        }

        protected virtual bool Asset_GetPrecision(IStackAccessor stack)
        {
            var asset = stack.Pop<Asset>();
            if (asset == null) return false;

            stack.Push((int)asset.Precision);

            return true;
        }

        protected virtual bool Asset_GetOwner(IStackAccessor stack)
        {
            var asset = stack.Pop<Asset>();
            if (asset == null) return false;

            // TODO: it was EncodePoint before. Check with NEO
            stack.Push(asset.Owner.EncodedData);

            return true;
        }

        protected virtual bool Asset_GetAdmin(IStackAccessor stack)
        {
            var asset = stack.Pop<Asset>();
            if (asset == null) return false;

            stack.Push(asset.Admin.ToArray());

            return true;
        }

        protected virtual bool Asset_GetIssuer(IStackAccessor stack)
        {
            var asset = stack.Pop<Asset>();
            if (asset == null) return false;

            stack.Push(asset.Issuer.ToArray());

            return true;
        }

        protected virtual bool Contract_GetScript(IStackAccessor stack)
        {
            var contract = stack.Pop<Contract>();
            if (contract == null) return false;

            stack.Push(contract.Script);

            return true;
        }

        protected virtual bool Contract_IsPayable(IStackAccessor stack)
        {
            var contract = stack.Pop<Contract>();
            if (contract == null) return false;

            stack.Push(contract.Payable);

            return true;
        }

        protected virtual bool Storage_GetContext(IStackAccessor stack)
        {
            // TODO: acquire script hash from engine
            //stack.Push(new StorageContext
            //{
            //    ScriptHash = new UInt160(engine.CurrentContext.ScriptHash),
            //    IsReadOnly = false
            //});

            return true;
        }

        protected virtual bool Storage_GetReadOnlyContext(IStackAccessor stack)
        {
            // TODO: acquire script hash from engine
            //stack.Push(new StorageContext
            //{
            //    ScriptHash = new UInt160(engine.CurrentContext.ScriptHash),
            //    IsReadOnly = true
            //});

            return true;
        }

        protected virtual bool Storage_Get(IStackAccessor stack)
        {
            var storageContext = stack.Pop<StorageContext>();
            if (storageContext == null) return false;
            if (!CheckStorageContext(storageContext)) return false;

            var key = stack.PopByteArray();
            var item = Storages.TryGet(new StorageKey
            {
                ScriptHash = storageContext.ScriptHash,
                Key = key
            });

            stack.Push(item?.Value ?? Array.Empty<byte>());

            return true;
        }

        protected virtual bool Storage_Find(IStackAccessor stack)
        {
            var storageContext = stack.Pop<StorageContext>();
            if (storageContext == null) return false;
            if (!CheckStorageContext(storageContext)) return false;

            var prefix = stack.PopByteArray();
            byte[] prefixKey;

            using (var ms = new MemoryStream())
            {
                var index = 0;
                var remain = prefix.Length;

                while (remain >= 16)
                {
                    ms.Write(prefix, index, 16);
                    ms.WriteByte(0);
                    index += 16;
                    remain -= 16;
                }

                if (remain > 0)
                    ms.Write(prefix, index, remain);

                prefixKey = storageContext.ScriptHash.ToArray().Concat(ms.ToArray()).ToArray();
            }

            var iterator = Storages.Find(prefixKey)
                .Where(p => p.Key.Key.Take(prefix.Length).SequenceEqual(prefix))
                .GetEnumerator();

            stack.Push(iterator);
            _disposables.Add(iterator);

            return true;
        }

        protected virtual bool StorageContext_AsReadOnly(IStackAccessor stack)
        {
            var storageContext = stack.Pop<StorageContext>();
            if (storageContext == null) return false;
            if (!storageContext.IsReadOnly)
                storageContext = new StorageContext
                {
                    ScriptHash = storageContext.ScriptHash,
                    IsReadOnly = true
                };

            stack.Push(storageContext);

            return true;
        }

        //protected virtual bool Enumerator_Create(IStackAccessor stack)
        //{
        //    var array = stack.PopArray();
        //    if (array == null) return false;

        //    stack.Push(array.GetEnumerator());

        //    return true;
        //}

        //protected virtual bool Enumerator_Next(IStackAccessor stack)
        //{
        //    var enumerator = stack.Pop<IEnumerator>();
        //    if (enumerator == null) return false;

        //    enumerator.MoveNext();

        //    stack.Push(enumerator);

        //    return true;
        //}

        //protected virtual bool Enumerator_Value(IStackAccessor stack)
        //{
        //    var enumerator = stack.Pop<IEnumerator>();
        //    if (enumerator == null) return false;

        //    stack.Push(enumerator.Current);

        //    return true;
        //}

        //protected virtual bool Enumerator_Concat(IStackAccessor stack)
        //{
        //    var enumerator1 = stack.Pop<IEnumerator>();
        //    if (enumerator1 == null) return false;

        //    var enumerator2 = stack.Pop<IEnumerator>();
        //    if (enumerator2 == null) return false;

        //    IEnumerator result = new ConcatEnumerator(first, second);
        //    stack.Push(StackItem.FromInterface(result));
        //    return true;
        //}

        //protected virtual bool Iterator_Create(IStackAccessor stack)
        //{
        //    if (stack.Pop() is Map map)
        //    {
        //        IIterator iterator = new MapWrapper(map);
        //        stack.Push(StackItem.FromInterface(iterator));
        //        return true;
        //    }
        //    return false;
        //}

        //protected virtual bool Iterator_Key(IStackAccessor stack)
        //{
        //    if (stack.Pop() is InteropInterface _interface)
        //    {
        //        IIterator iterator = _interface.GetInterface<IIterator>();
        //        stack.Push(iterator.Key());
        //        return true;
        //    }
        //    return false;
        //}

        //protected virtual bool Iterator_Keys(IStackAccessor stack)
        //{
        //    if (stack.Pop() is InteropInterface _interface)
        //    {
        //        IIterator iterator = _interface.GetInterface<IIterator>();
        //        stack.Push(StackItem.FromInterface(new IteratorKeysWrapper(iterator)));
        //        return true;
        //    }
        //    return false;
        //}

        //protected virtual bool Iterator_Values(IStackAccessor stack)
        //{
        //    if (stack.Pop() is InteropInterface _interface)
        //    {
        //        IIterator iterator = _interface.GetInterface<IIterator>();
        //        stack.Push(StackItem.FromInterface(new IteratorValuesWrapper(iterator)));
        //        return true;
        //    }
        //    return false;
        //}
    }
}