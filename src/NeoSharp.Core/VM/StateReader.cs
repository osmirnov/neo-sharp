using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using NeoSharp.Core.Blockchain.Repositories;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Extensions;
using NeoSharp.Core.Models;
using NeoSharp.Core.Network;
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
        private readonly InteropService _interopService;

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
            InteropService interopService,
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
            _interopService = interopService;

            //Standard Library
            interopService.RegisterStackCall("System.Runtime.GetTrigger", Runtime_GetTrigger);
            interopService.RegisterStackCall("System.Runtime.CheckWitness", Runtime_CheckWitness);
            interopService.Register("System.Runtime.Notify", Runtime_Notify);
            interopService.Register("System.Runtime.Log", Runtime_Log);
            interopService.RegisterStackCall("System.Runtime.GetTime", Runtime_GetTime);
            interopService.RegisterStackCall("System.Blockchain.GetHeight", Blockchain_GetHeight);
            interopService.RegisterStackCall("System.Blockchain.GetHeader", Blockchain_GetHeader);
            interopService.RegisterStackCall("System.Blockchain.GetBlock", Blockchain_GetBlock);
            interopService.RegisterStackCall("System.Blockchain.GetTransaction", Blockchain_GetTransaction);
            interopService.RegisterStackCall("System.Blockchain.GetTransactionHeight", Blockchain_GetTransactionHeight);
            interopService.RegisterStackCall("System.Blockchain.GetContract", Blockchain_GetContract);
            interopService.RegisterStackCall("System.Header.GetIndex", Header_GetIndex);
            interopService.RegisterStackCall("System.Header.GetHash", Header_GetHash);
            interopService.RegisterStackCall("System.Header.GetPrevHash", Header_GetPrevHash);
            interopService.RegisterStackCall("System.Header.GetTimestamp", Header_GetTimestamp);
            interopService.RegisterStackCall("System.Block.GetTransactionCount", Block_GetTransactionCount);
            interopService.RegisterStackCall("System.Block.GetTransactions", Block_GetTransactions);
            interopService.RegisterStackCall("System.Block.GetTransaction", Block_GetTransaction);
            interopService.RegisterStackCall("System.Transaction.GetHash", Transaction_GetHash);
            interopService.Register("System.Storage.GetContext", Storage_GetContext);
            interopService.Register("System.Storage.GetReadOnlyContext", Storage_GetReadOnlyContext);
            interopService.RegisterStackCall("System.Storage.Get", Storage_Get);
            interopService.RegisterStackCall("System.StorageContext.AsReadOnly", StorageContext_AsReadOnly);

            //Neo Specified
            interopService.RegisterStackCall("Neo.Blockchain.GetAccount", Blockchain_GetAccount);
            interopService.RegisterStackCall("Neo.Blockchain.GetValidators", Blockchain_GetValidators);
            interopService.RegisterStackCall("Neo.Blockchain.GetAsset", Blockchain_GetAsset);
            interopService.RegisterStackCall("Neo.Header.GetVersion", Header_GetVersion);
            interopService.RegisterStackCall("Neo.Header.GetMerkleRoot", Header_GetMerkleRoot);
            interopService.RegisterStackCall("Neo.Header.GetConsensusData", Header_GetConsensusData);
            interopService.RegisterStackCall("Neo.Header.GetNextConsensus", Header_GetNextConsensus);
            interopService.RegisterStackCall("Neo.Transaction.GetType", Transaction_GetType);
            interopService.RegisterStackCall("Neo.Transaction.GetAttributes", Transaction_GetAttributes);
            interopService.RegisterStackCall("Neo.Transaction.GetInputs", Transaction_GetInputs);
            interopService.RegisterStackCall("Neo.Transaction.GetOutputs", Transaction_GetOutputs);
            interopService.RegisterStackCall("Neo.Transaction.GetReferences", Transaction_GetReferences);
            interopService.RegisterStackCall("Neo.Transaction.GetUnspentCoins", Transaction_GetUnspentCoins);
            interopService.RegisterStackCall("Neo.InvocationTransaction.GetScript", InvocationTransaction_GetScript);
            interopService.RegisterStackCall("Neo.Attribute.GetUsage", Attribute_GetUsage);
            interopService.RegisterStackCall("Neo.Attribute.GetData", Attribute_GetData);
            interopService.RegisterStackCall("Neo.Input.GetHash", Input_GetHash);
            interopService.RegisterStackCall("Neo.Input.GetIndex", Input_GetIndex);
            interopService.RegisterStackCall("Neo.Output.GetAssetId", Output_GetAssetId);
            interopService.RegisterStackCall("Neo.Output.GetValue", Output_GetValue);
            interopService.RegisterStackCall("Neo.Output.GetScriptHash", Output_GetScriptHash);
            interopService.RegisterStackCall("Neo.Account.GetScriptHash", Account_GetScriptHash);
            interopService.RegisterStackCall("Neo.Account.GetVotes", Account_GetVotes);
            interopService.RegisterStackCall("Neo.Account.GetBalance", Account_GetBalance);
            interopService.RegisterStackCall("Neo.Asset.GetAssetId", Asset_GetAssetId);
            interopService.RegisterStackCall("Neo.Asset.GetAssetType", Asset_GetAssetType);
            interopService.RegisterStackCall("Neo.Asset.GetAmount", Asset_GetAmount);
            interopService.RegisterStackCall("Neo.Asset.GetAvailable", Asset_GetAvailable);
            interopService.RegisterStackCall("Neo.Asset.GetPrecision", Asset_GetPrecision);
            interopService.RegisterStackCall("Neo.Asset.GetOwner", Asset_GetOwner);
            interopService.RegisterStackCall("Neo.Asset.GetAdmin", Asset_GetAdmin);
            interopService.RegisterStackCall("Neo.Asset.GetIssuer", Asset_GetIssuer);
            interopService.RegisterStackCall("Neo.Contract.GetScript", Contract_GetScript);
            interopService.RegisterStackCall("Neo.Contract.IsPayable", Contract_IsPayable);
            interopService.RegisterStackCall("Neo.Storage.Find", Storage_Find);
            // TODO: APIs for enumeration and iteration
            //interopService.RegisterStackCall("Neo.Enumerator.Create", Enumerator_Create);
            //interopService.RegisterStackCall("Neo.Enumerator.Next", Enumerator_Next);
            //interopService.RegisterStackCall("Neo.Enumerator.Value", Enumerator_Value);
            //interopService.RegisterStackCall("Neo.Enumerator.Concat", Enumerator_Concat);
            //interopService.RegisterStackCall("Neo.Iterator.Create", Iterator_Create);
            //interopService.RegisterStackCall("Neo.Iterator.Key", Iterator_Key);
            //interopService.RegisterStackCall("Neo.Iterator.Keys", Iterator_Keys);
            //interopService.RegisterStackCall("Neo.Iterator.Values", Iterator_Values);

            #region Aliases
            //interopService.RegisterStackCall("Neo.Iterator.Next", Enumerator_Next);
            //interopService.RegisterStackCall("Neo.Iterator.Value", Enumerator_Value);
            #endregion

            #region Old APIs
            interopService.RegisterStackCall("Neo.Runtime.GetTrigger", Runtime_GetTrigger);
            interopService.RegisterStackCall("Neo.Runtime.CheckWitness", Runtime_CheckWitness);
            interopService.RegisterStackCall("AntShares.Runtime.CheckWitness", Runtime_CheckWitness);
            interopService.Register("Neo.Runtime.Notify", Runtime_Notify);
            interopService.Register("AntShares.Runtime.Notify", Runtime_Notify);
            interopService.Register("Neo.Runtime.Log", Runtime_Log);
            interopService.Register("AntShares.Runtime.Log", Runtime_Log);
            interopService.RegisterStackCall("Neo.Runtime.GetTime", Runtime_GetTime);
            interopService.RegisterStackCall("Neo.Blockchain.GetHeight", Blockchain_GetHeight);
            interopService.RegisterStackCall("AntShares.Blockchain.GetHeight", Blockchain_GetHeight);
            interopService.RegisterStackCall("Neo.Blockchain.GetHeader", Blockchain_GetHeader);
            interopService.RegisterStackCall("AntShares.Blockchain.GetHeader", Blockchain_GetHeader);
            interopService.RegisterStackCall("Neo.Blockchain.GetBlock", Blockchain_GetBlock);
            interopService.RegisterStackCall("AntShares.Blockchain.GetBlock", Blockchain_GetBlock);
            interopService.RegisterStackCall("Neo.Blockchain.GetTransaction", Blockchain_GetTransaction);
            interopService.RegisterStackCall("AntShares.Blockchain.GetTransaction", Blockchain_GetTransaction);
            interopService.RegisterStackCall("Neo.Blockchain.GetTransactionHeight", Blockchain_GetTransactionHeight);
            interopService.RegisterStackCall("AntShares.Blockchain.GetAccount", Blockchain_GetAccount);
            interopService.RegisterStackCall("AntShares.Blockchain.GetValidators", Blockchain_GetValidators);
            interopService.RegisterStackCall("AntShares.Blockchain.GetAsset", Blockchain_GetAsset);
            interopService.RegisterStackCall("Neo.Blockchain.GetContract", Blockchain_GetContract);
            interopService.RegisterStackCall("AntShares.Blockchain.GetContract", Blockchain_GetContract);
            interopService.RegisterStackCall("Neo.Header.GetIndex", Header_GetIndex);
            interopService.RegisterStackCall("Neo.Header.GetHash", Header_GetHash);
            interopService.RegisterStackCall("AntShares.Header.GetHash", Header_GetHash);
            interopService.RegisterStackCall("AntShares.Header.GetVersion", Header_GetVersion);
            interopService.RegisterStackCall("Neo.Header.GetPrevHash", Header_GetPrevHash);
            interopService.RegisterStackCall("AntShares.Header.GetPrevHash", Header_GetPrevHash);
            interopService.RegisterStackCall("AntShares.Header.GetMerkleRoot", Header_GetMerkleRoot);
            interopService.RegisterStackCall("Neo.Header.GetTimestamp", Header_GetTimestamp);
            interopService.RegisterStackCall("AntShares.Header.GetTimestamp", Header_GetTimestamp);
            interopService.RegisterStackCall("AntShares.Header.GetConsensusData", Header_GetConsensusData);
            interopService.RegisterStackCall("AntShares.Header.GetNextConsensus", Header_GetNextConsensus);
            interopService.RegisterStackCall("Neo.Block.GetTransactionCount", Block_GetTransactionCount);
            interopService.RegisterStackCall("AntShares.Block.GetTransactionCount", Block_GetTransactionCount);
            interopService.RegisterStackCall("Neo.Block.GetTransactions", Block_GetTransactions);
            interopService.RegisterStackCall("AntShares.Block.GetTransactions", Block_GetTransactions);
            interopService.RegisterStackCall("Neo.Block.GetTransaction", Block_GetTransaction);
            interopService.RegisterStackCall("AntShares.Block.GetTransaction", Block_GetTransaction);
            interopService.RegisterStackCall("Neo.Transaction.GetHash", Transaction_GetHash);
            interopService.RegisterStackCall("AntShares.Transaction.GetHash", Transaction_GetHash);
            interopService.RegisterStackCall("AntShares.Transaction.GetType", Transaction_GetType);
            interopService.RegisterStackCall("AntShares.Transaction.GetAttributes", Transaction_GetAttributes);
            interopService.RegisterStackCall("AntShares.Transaction.GetInputs", Transaction_GetInputs);
            interopService.RegisterStackCall("AntShares.Transaction.GetOutputs", Transaction_GetOutputs);
            interopService.RegisterStackCall("AntShares.Transaction.GetReferences", Transaction_GetReferences);
            interopService.RegisterStackCall("AntShares.Attribute.GetUsage", Attribute_GetUsage);
            interopService.RegisterStackCall("AntShares.Attribute.GetData", Attribute_GetData);
            interopService.RegisterStackCall("AntShares.Input.GetHash", Input_GetHash);
            interopService.RegisterStackCall("AntShares.Input.GetIndex", Input_GetIndex);
            interopService.RegisterStackCall("AntShares.Output.GetAssetId", Output_GetAssetId);
            interopService.RegisterStackCall("AntShares.Output.GetValue", Output_GetValue);
            interopService.RegisterStackCall("AntShares.Output.GetScriptHash", Output_GetScriptHash);
            interopService.RegisterStackCall("AntShares.Account.GetScriptHash", Account_GetScriptHash);
            interopService.RegisterStackCall("AntShares.Account.GetVotes", Account_GetVotes);
            interopService.RegisterStackCall("AntShares.Account.GetBalance", Account_GetBalance);
            interopService.RegisterStackCall("AntShares.Asset.GetAssetId", Asset_GetAssetId);
            interopService.RegisterStackCall("AntShares.Asset.GetAssetType", Asset_GetAssetType);
            interopService.RegisterStackCall("AntShares.Asset.GetAmount", Asset_GetAmount);
            interopService.RegisterStackCall("AntShares.Asset.GetAvailable", Asset_GetAvailable);
            interopService.RegisterStackCall("AntShares.Asset.GetPrecision", Asset_GetPrecision);
            interopService.RegisterStackCall("AntShares.Asset.GetOwner", Asset_GetOwner);
            interopService.RegisterStackCall("AntShares.Asset.GetAdmin", Asset_GetAdmin);
            interopService.RegisterStackCall("AntShares.Asset.GetIssuer", Asset_GetIssuer);
            interopService.RegisterStackCall("AntShares.Contract.GetScript", Contract_GetScript);
            interopService.Register("Neo.Storage.GetContext", Storage_GetContext);
            interopService.Register("AntShares.Storage.GetContext", Storage_GetContext);
            interopService.Register("Neo.Storage.GetReadOnlyContext", Storage_GetReadOnlyContext);
            interopService.RegisterStackCall("Neo.Storage.Get", Storage_Get);
            interopService.RegisterStackCall("AntShares.Storage.Get", Storage_Get);
            interopService.RegisterStackCall("Neo.StorageContext.AsReadOnly", StorageContext_AsReadOnly);
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

        protected virtual bool Runtime_GetTrigger(Stack stack)
        {
            stack.Push((int)_trigger);
            return true;
        }

        protected bool CheckWitness(ExecutionEngineBase engine, UInt160 hash)
        {
            // TODO:
            //IVerifiable container = (IVerifiable)engine.MessageProvider;
            //UInt160[] _hashes_for_verifying = container.GetScriptHashesForVerifying();
            //return _hashes_for_verifying.Contains(hash);
            return true;
        }

        protected bool CheckWitness(ExecutionEngineBase engine, ECPoint pubkey)
        {
            // TODO:
            //return CheckWitness(engine, Contract.CreateSignatureRedeemScript(pubkey).ToScriptHash());
            return true;
        }

        protected virtual bool Runtime_CheckWitness(Stack stack)
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

        protected virtual bool Runtime_Notify(ExecutionEngineBase engine)
        {
            var ctx = engine.CurrentContext;
            if (ctx == null) return false;

            var state = ctx.EvaluationStack.PopObject<StackItemBase>();

            _interopService.RaiseOnNotify(new NotifyEventArgs(ctx.ScriptHash.ToArray(), state));

            return true;
        }

        protected virtual bool Runtime_Log(ExecutionEngineBase engine)
        {
            var ctx = engine.CurrentContext;
            if (ctx == null) return false;

            var message = Encoding.UTF8.GetString(ctx.EvaluationStack.PopByteArray());

            _interopService.RaiseOnLog(new LogEventArgs(ctx.ScriptHash.ToArray(), message));

            return true;
        }

        protected virtual bool Runtime_GetTime(Stack stack)
        {
            stack.Push(_blockchainContext.LastBlockHeader.Timestamp + 15);
            return true;
        }

        protected virtual bool Blockchain_GetHeight(Stack stack)
        {
            stack.Push(_blockchainContext.CurrentBlock.Index);
            return true;
        }

        protected virtual bool Blockchain_GetHeader(Stack stack)
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

            stack.PushObject(blockHeader);

            return true;
        }

        protected virtual bool Blockchain_GetBlock(Stack stack)
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

            stack.PushObject(block);

            return true;
        }

        protected virtual bool Blockchain_GetTransaction(Stack stack)
        {
            var hash = stack.PopByteArray();
            var transaction = _transactionRepository.GetTransaction(new UInt256(hash)).Result;

            stack.PushObject(transaction);

            return true;
        }

        protected virtual bool Blockchain_GetTransactionHeight(Stack stack)
        {
            var hash = stack.PopByteArray();
            // TODO: looks like we need an index transaction in the block;
            var height = 0;
            // var transaction = _transactionRepository.GetTransaction(new UInt256(hash)).Result;

            stack.Push(height);

            return true;
        }

        protected virtual bool Blockchain_GetAccount(Stack stack)
        {
            var hash = new UInt160(stack.PopByteArray());
            var account = Accounts.GetOrAdd(hash, () => new Account(hash));
            stack.PushObject(account);
            return true;
        }

        protected virtual bool Blockchain_GetValidators(Stack stack)
        {
            // TODO: looks like we need to get all validators
            //ECPoint[] validators = _blockchain.GetValidators();
            //stack.Push(validators.Select(p => (StackItem)p.EncodePoint(true)).ToArray());
            return true;
        }

        protected virtual bool Blockchain_GetAsset(Stack stack)
        {
            var hash = new UInt256(stack.PopByteArray());
            var asset = Assets.TryGet(hash);
            if (asset == null) return false;
            stack.PushObject(asset);
            return true;
        }

        protected virtual bool Blockchain_GetContract(Stack stack)
        {
            var hash = new UInt160(stack.PopByteArray());
            var contract = Contracts.TryGet(hash);
            if (contract == null)
                stack.Push(Array.Empty<byte>());
            else
                stack.PushObject(contract);
            return true;
        }

        protected virtual bool Header_GetIndex(Stack stack)
        {
            var blockBase = stack.PopObject<BlockBase>();
            if (blockBase == null) return false;

            stack.Push(blockBase.Index);

            return true;
        }

        protected virtual bool Header_GetHash(Stack stack)
        {
            var blockBase = stack.PopObject<BlockBase>();
            if (blockBase == null) return false;

            stack.Push(blockBase.Hash.ToArray());

            return true;
        }

        protected virtual bool Header_GetVersion(Stack stack)
        {
            var blockBase = stack.PopObject<BlockBase>();
            if (blockBase == null) return false;

            stack.Push(blockBase.Version);

            return true;
        }

        protected virtual bool Header_GetPrevHash(Stack stack)
        {
            var blockBase = stack.PopObject<BlockBase>();
            if (blockBase == null) return false;

            stack.Push(blockBase.PreviousBlockHash.ToArray());

            return true;
        }

        protected virtual bool Header_GetMerkleRoot(Stack stack)
        {
            // TODO: Should MerkleRoot be in BlockBase?
            var blockBase = stack.PopObject<BlockHeader>();
            if (blockBase == null) return false;

            stack.Push(blockBase.MerkleRoot.ToArray());

            return true;
        }

        protected virtual bool Header_GetTimestamp(Stack stack)
        {
            var blockBase = stack.PopObject<BlockBase>();
            if (blockBase == null) return false;

            stack.Push(blockBase.Timestamp);

            return true;
        }

        protected virtual bool Header_GetConsensusData(Stack stack)
        {
            var blockBase = stack.PopObject<BlockBase>();
            if (blockBase == null) return false;

            stack.Push(blockBase.ConsensusData);

            return true;
        }

        protected virtual bool Header_GetNextConsensus(Stack stack)
        {
            var blockBase = stack.PopObject<BlockBase>();
            if (blockBase == null) return false;

            stack.Push(blockBase.NextConsensus.ToArray());

            return true;
        }

        protected virtual bool Block_GetTransactionCount(Stack stack)
        {
            var block = stack.PopObject<Block>();
            if (block == null) return false;

            stack.Push(block.Transactions.Length);

            return true;
        }

        protected virtual bool Block_GetTransactions(Stack stack)
        {
            var block = stack.PopObject<Block>();
            if (block == null) return false;
            if (block.Transactions.Length > EngineMaxArraySize) return false;

            stack.Push(block.Transactions);

            return true;
        }

        protected virtual bool Block_GetTransaction(Stack stack)
        {
            var block = stack.PopObject<Block>();
            if (block == null) return false;

            var index = (int)stack.PopBigInteger();
            if (index < 0 || index >= block.Transactions.Length) return false;

            var transaction = block.Transactions[index];

            stack.PushObject(transaction);

            return true;
        }

        protected virtual bool Transaction_GetHash(Stack stack)
        {
            var transaction = stack.PopObject<Transaction>();
            if (transaction == null) return false;

            stack.Push(transaction.Hash.ToArray());

            return true;
        }

        protected virtual bool Transaction_GetType(Stack stack)
        {
            var transaction = stack.PopObject<Transaction>();
            if (transaction == null) return false;

            stack.Push((int)transaction.Type);

            return true;
        }

        protected virtual bool Transaction_GetAttributes(Stack stack)
        {
            var transaction = stack.PopObject<Transaction>();
            if (transaction == null) return false;
            if (transaction.Attributes.Length > EngineMaxArraySize)
                return false;

            stack.Push(transaction.Attributes);

            return true;
        }

        protected virtual bool Transaction_GetInputs(Stack stack)
        {
            var transaction = stack.PopObject<Transaction>();
            if (transaction == null) return false;
            if (transaction.Inputs.Length > EngineMaxArraySize)
                return false;

            stack.Push(transaction.Inputs);

            return true;
        }

        protected virtual bool Transaction_GetOutputs(Stack stack)
        {
            var transaction = stack.PopObject<Transaction>();
            if (transaction == null) return false;
            if (transaction.Outputs.Length > EngineMaxArraySize)
                return false;

            stack.Push(transaction.Outputs);

            return true;
        }

        protected virtual bool Transaction_GetReferences(Stack stack)
        {
            var transaction = stack.PopObject<Transaction>();
            if (transaction == null) return false;
            // TODO: Check refs length?
            if (transaction.Inputs.Length > EngineMaxArraySize)
                return false;

            var references = _transactionRepository.GetReferences(transaction).Result;
            stack.Push(transaction.Inputs.Select(i => references[i]).ToArray());

            return true;
        }

        protected virtual bool Transaction_GetUnspentCoins(Stack stack)
        {
            var transaction = stack.PopObject<Transaction>();
            if (transaction == null) return false;

            var outputs = _transactionRepository.GetUnspent(transaction.Hash).Result;
            if (outputs.Count() > EngineMaxArraySize)
                return false;

            stack.Push(outputs.ToArray());

            return true;
        }

        protected virtual bool InvocationTransaction_GetScript(Stack stack)
        {
            var transaction = stack.PopObject<InvocationTransaction>();
            if (transaction == null) return false;

            stack.Push(transaction.Script);

            return true;
        }

        protected virtual bool Attribute_GetUsage(Stack stack)
        {
            var transactionAttr = stack.PopObject<TransactionAttribute>();
            if (transactionAttr == null) return false;

            stack.Push((int)transactionAttr.Usage);

            return true;
        }

        protected virtual bool Attribute_GetData(Stack stack)
        {
            var transactionAttr = stack.PopObject<TransactionAttribute>();
            if (transactionAttr == null) return false;

            stack.Push(transactionAttr.Data);

            return true;
        }

        protected virtual bool Input_GetHash(Stack stack)
        {
            var coinReference = stack.PopObject<CoinReference>();
            if (coinReference == null) return false;

            stack.Push(coinReference.PrevHash.ToArray());

            return true;
        }

        protected virtual bool Input_GetIndex(Stack stack)
        {
            var coinReference = stack.PopObject<CoinReference>();
            if (coinReference == null) return false;

            stack.Push((int)coinReference.PrevIndex);

            return true;
        }

        protected virtual bool Output_GetAssetId(Stack stack)
        {
            var transactionOutput = stack.PopObject<TransactionOutput>();
            if (transactionOutput == null) return false;

            stack.Push(transactionOutput.AssetId.ToArray());

            return true;
        }

        protected virtual bool Output_GetValue(Stack stack)
        {
            var transactionOutput = stack.PopObject<TransactionOutput>();
            if (transactionOutput == null) return false;

            stack.Push(transactionOutput.Value.Value);

            return true;
        }

        protected virtual bool Output_GetScriptHash(Stack stack)
        {
            var transactionOutput = stack.PopObject<TransactionOutput>();
            if (transactionOutput == null) return false;

            stack.Push(transactionOutput.ScriptHash.ToArray());

            return true;
        }

        protected virtual bool Account_GetScriptHash(Stack stack)
        {
            var account = stack.PopObject<Account>();
            if (account == null) return false;

            stack.Push(account.ScriptHash.ToArray());

            return true;
        }

        protected virtual bool Account_GetVotes(Stack stack)
        {
            var account = stack.PopObject<Account>();
            if (account == null) return false;

            // TODO: it was EncodePoint before. Check with NEO
            account.Votes.Select(v => v.EncodedData).ForEach(stack.Push);

            return true;
        }

        protected virtual bool Account_GetBalance(Stack stack)
        {
            var account = stack.PopObject<Account>();
            if (account == null) return false;

            var assetId = new UInt256(stack.PopByteArray());
            var balance = account.Balances.TryGetValue(assetId, out var value) ? value : Fixed8.Zero;

            stack.Push(balance.Value);

            return true;
        }

        protected virtual bool Asset_GetAssetId(Stack stack)
        {
            var asset = stack.PopObject<Asset>();
            if (asset == null) return false;

            stack.Push(asset.Id.ToArray());

            return true;
        }

        protected virtual bool Asset_GetAssetType(Stack stack)
        {
            var asset = stack.PopObject<Asset>();
            if (asset == null) return false;

            stack.Push((int)asset.AssetType);

            return true;
        }

        protected virtual bool Asset_GetAmount(Stack stack)
        {
            var asset = stack.PopObject<Asset>();
            if (asset == null) return false;

            stack.Push(asset.Amount.Value);

            return true;
        }

        protected virtual bool Asset_GetAvailable(Stack stack)
        {
            var asset = stack.PopObject<Asset>();
            if (asset == null) return false;

            stack.Push(asset.Available.Value);

            return true;
        }

        protected virtual bool Asset_GetPrecision(Stack stack)
        {
            var asset = stack.PopObject<Asset>();
            if (asset == null) return false;

            stack.Push((int)asset.Precision);

            return true;
        }

        protected virtual bool Asset_GetOwner(Stack stack)
        {
            var asset = stack.PopObject<Asset>();
            if (asset == null) return false;

            // TODO: it was EncodePoint before. Check with NEO
            stack.Push(asset.Owner.EncodedData);

            return true;
        }

        protected virtual bool Asset_GetAdmin(Stack stack)
        {
            var asset = stack.PopObject<Asset>();
            if (asset == null) return false;

            stack.Push(asset.Admin.ToArray());

            return true;
        }

        protected virtual bool Asset_GetIssuer(Stack stack)
        {
            var asset = stack.PopObject<Asset>();
            if (asset == null) return false;

            stack.Push(asset.Issuer.ToArray());

            return true;
        }

        protected virtual bool Contract_GetScript(Stack stack)
        {
            var contract = stack.PopObject<Contract>();
            if (contract == null) return false;

            stack.Push(contract.Script);

            return true;
        }

        protected virtual bool Contract_IsPayable(Stack stack)
        {
            var contract = stack.PopObject<Contract>();
            if (contract == null) return false;

            stack.Push(contract.Payable);

            return true;
        }

        protected virtual bool Storage_GetContext(ExecutionEngineBase engine)
        {
            var ctx = engine.CurrentContext;
            if (ctx == null) return false;

            ctx.EvaluationStack.PushObject(new StorageContext
            {
                ScriptHash = new UInt160(ctx.ScriptHash),
                IsReadOnly = false
            });

            return true;
        }

        protected virtual bool Storage_GetReadOnlyContext(ExecutionEngineBase engine)
        {
            var ctx = engine.CurrentContext;
            if (ctx == null) return false;

            ctx.EvaluationStack.PushObject(new StorageContext
            {
                ScriptHash = new UInt160(ctx.ScriptHash),
                IsReadOnly = true
            });

            return true;
        }

        protected virtual bool Storage_Get(Stack stack)
        {
            var storageContext = stack.PopObject<StorageContext>();
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

        protected virtual bool Storage_Find(Stack stack)
        {
            var storageContext = stack.PopObject<StorageContext>();
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

            stack.PushObject(iterator);
            _disposables.Add(iterator);

            return true;
        }

        protected virtual bool StorageContext_AsReadOnly(Stack stack)
        {
            var storageContext = stack.PopObject<StorageContext>();
            if (storageContext == null) return false;
            if (!storageContext.IsReadOnly)
                storageContext = new StorageContext
                {
                    ScriptHash = storageContext.ScriptHash,
                    IsReadOnly = true
                };

            stack.PushObject(storageContext);

            return true;
        }

        //protected virtual bool Enumerator_Create(Stack stack)
        //{
        //    var array = stack.PopArray();
        //    if (array == null) return false;

        //    stack.Push(array.GetEnumerator());

        //    return true;
        //}

        //protected virtual bool Enumerator_Next(Stack stack)
        //{
        //    var enumerator = stack.Pop<IEnumerator>();
        //    if (enumerator == null) return false;

        //    enumerator.MoveNext();

        //    stack.Push(enumerator);

        //    return true;
        //}

        //protected virtual bool Enumerator_Value(Stack stack)
        //{
        //    var enumerator = stack.Pop<IEnumerator>();
        //    if (enumerator == null) return false;

        //    stack.Push(enumerator.Current);

        //    return true;
        //}

        //protected virtual bool Enumerator_Concat(Stack stack)
        //{
        //    var enumerator1 = stack.Pop<IEnumerator>();
        //    if (enumerator1 == null) return false;

        //    var enumerator2 = stack.Pop<IEnumerator>();
        //    if (enumerator2 == null) return false;

        //    IEnumerator result = new ConcatEnumerator(first, second);
        //    stack.Push(StackItem.FromInterface(result));
        //    return true;
        //}

        //protected virtual bool Iterator_Create(Stack stack)
        //{
        //    if (stack.Pop() is Map map)
        //    {
        //        IIterator iterator = new MapWrapper(map);
        //        stack.Push(StackItem.FromInterface(iterator));
        //        return true;
        //    }
        //    return false;
        //}

        //protected virtual bool Iterator_Key(Stack stack)
        //{
        //    if (stack.Pop() is InteropInterface _interface)
        //    {
        //        IIterator iterator = _interface.GetInterface<IIterator>();
        //        stack.Push(iterator.Key());
        //        return true;
        //    }
        //    return false;
        //}

        //protected virtual bool Iterator_Keys(Stack stack)
        //{
        //    if (stack.Pop() is InteropInterface _interface)
        //    {
        //        IIterator iterator = _interface.GetInterface<IIterator>();
        //        stack.Push(StackItem.FromInterface(new IteratorKeysWrapper(iterator)));
        //        return true;
        //    }
        //    return false;
        //}

        //protected virtual bool Iterator_Values(Stack stack)
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