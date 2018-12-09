﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NeoSharp.Core.Blockchain.Repositories;
using NeoSharp.Core.Extensions;
using NeoSharp.Core.Models;
using NeoSharp.Core.Network;
using NeoSharp.Core.SmartContract;
using NeoSharp.Types;
using NeoSharp.VM;

namespace NeoSharp.Core.VM
{
    public class StateMachine : StateReader
    {
        private readonly Block _persistingBlock;
        private readonly IBlockchainContext _blockchainContext;
        private readonly DataCache<UInt160, Account> _accounts;
        private readonly DataCache<UInt256, Asset> _assets;
        private readonly DataCache<UInt160, Contract> _contracts;
        private readonly DataCache<StorageKey, StorageValue> _storages;

        private readonly Dictionary<UInt160, UInt160> _contractsCreated = new Dictionary<UInt160, UInt160>();

        public StateMachine(
            Block persistingBlock,
            DataCache<UInt160, Account> accounts,
            DataCache<UInt256, Asset> assets,
            DataCache<UInt160, Contract> contracts,
            DataCache<StorageKey, StorageValue> storages,
            InteropService interopService,
            IBlockchainContext blockchainContext,
            IBlockRepository blockRepository,
            ITransactionRepository transactionRepository,
            ETriggerType trigger)
        : base(accounts, assets, contracts, storages, interopService, blockchainContext, blockRepository, transactionRepository, trigger)
        {
            _persistingBlock = persistingBlock;
            _blockchainContext = blockchainContext;
            _accounts = accounts?.CreateSnapshot();
            _assets = assets?.CreateSnapshot();
            _contracts = contracts?.CreateSnapshot();
            _storages = storages?.CreateSnapshot();

            // Standard Library

            interopService.RegisterStackCall("System.Contract.GetStorageContext", Contract_GetStorageContext);
            interopService.RegisterStackCall("System.Contract.Destroy", Contract_Destroy);
            interopService.RegisterStackCall("System.Storage.Put", Storage_Put);
            interopService.RegisterStackCall("System.Storage.Delete", Storage_Delete);

            // Neo Specified

            interopService.RegisterStackCall("Neo.Asset.Create", Asset_Create);
            interopService.RegisterStackCall("Neo.Asset.Renew", Asset_Renew);
            interopService.RegisterStackCall("Neo.Contract.Create", Contract_Create);
            interopService.RegisterStackCall("Neo.Contract.Migrate", Contract_Migrate);

            #region Old APIs

            interopService.RegisterStackCall("AntShares.Asset.Create", Asset_Create);
            interopService.RegisterStackCall("AntShares.Asset.Renew", Asset_Renew);
            interopService.RegisterStackCall("AntShares.Contract.Create", Contract_Create);
            interopService.RegisterStackCall("AntShares.Contract.Migrate", Contract_Migrate);
            interopService.RegisterStackCall("Neo.Contract.GetStorageContext", Contract_GetStorageContext);
            interopService.RegisterStackCall("AntShares.Contract.GetStorageContext", Contract_GetStorageContext);
            interopService.RegisterStackCall("Neo.Contract.Destroy", Contract_Destroy);
            interopService.RegisterStackCall("AntShares.Contract.Destroy", Contract_Destroy);
            interopService.RegisterStackCall("Neo.Storage.Put", Storage_Put);
            interopService.RegisterStackCall("AntShares.Storage.Put", Storage_Put);
            interopService.RegisterStackCall("Neo.Storage.Delete", Storage_Delete);
            interopService.RegisterStackCall("AntShares.Storage.Delete", Storage_Delete);

            #endregion
        }

        public void Commit()
        {
            _accounts.Commit();
            _assets.Commit();
            _contracts.Commit();
            _storages.Commit();
        }

        protected override bool Runtime_GetTime(Stack stack)
        {
            stack.Push(_persistingBlock.Timestamp);
            return true;
        }

        private bool Asset_Create(Stack stack)
        {
            //InvocationTransaction tx = (InvocationTransaction)engine.ScriptContainer;
            //AssetType assetType = (AssetType)(byte)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();
            //if (!Enum.IsDefined(typeof(AssetType), assetType) || assetType == AssetType.CreditFlag || assetType == AssetType.DutyFlag || assetType == AssetType.GoverningToken || assetType == AssetType.UtilityToken)
            //    return false;
            //if (stack.PeekByteArray().Length > 1024)
            //    return false;
            //string name = Encoding.UTF8.GetString(stack.PopByteArray());
            //Fixed8 amount = new Fixed8((long)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger());
            //if (amount == Fixed8.Zero || amount < -Fixed8.Satoshi) return false;
            //if (assetType == AssetType.Invoice && amount != -Fixed8.Satoshi)
            //    return false;
            //byte precision = (byte)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();
            //if (precision > 8) return false;
            //if (assetType == AssetType.Share && precision != 0) return false;
            //if (amount != -Fixed8.Satoshi && amount.GetData() % (long)Math.Pow(10, 8 - precision) != 0)
            //    return false;
            //ECPoint owner = ECPoint.DecodePoint(stack.PopByteArray(), ECCurve.Secp256r1);
            //if (owner.IsInfinity) return false;
            //if (!CheckWitness(engine, owner))
            //    return false;
            //UInt160 admin = new UInt160(stack.PopByteArray());
            //UInt160 issuer = new UInt160(stack.PopByteArray());
            //AssetState asset = _assets.GetOrAdd(tx.Hash, () => new AssetState
            //{
            //    AssetId = tx.Hash,
            //    AssetType = assetType,
            //    Name = name,
            //    Amount = amount,
            //    Available = Fixed8.Zero,
            //    Precision = precision,
            //    Fee = Fixed8.Zero,
            //    FeeAddress = new UInt160(),
            //    Owner = owner,
            //    Admin = admin,
            //    Issuer = issuer,
            //    Expiration = Blockchain.Default.Height + 1 + 2000000,
            //    IsFrozen = false
            //});
            //engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(asset));
            return true;
        }

        private bool Asset_Renew(Stack stack)
        {
            var asset = stack.PopObject<Asset>();
            if (asset == null) return false;

            var years = (byte)stack.PopBigInteger();

            asset = _assets.GetAndChange(asset.Id);

            if (asset.Expiration < _blockchainContext.CurrentBlock.Index + 1)
                asset.Expiration = _blockchainContext.CurrentBlock.Index + 1;

            try
            {
                asset.Expiration = checked(asset.Expiration + years * 2000000u);
            }
            catch (OverflowException)
            {
                asset.Expiration = uint.MaxValue;
            }

            stack.Push(asset.Expiration);

            return true;
        }

        private bool Contract_Create(Stack stack)
        {
            var script = stack.PopByteArray();
            if (script.Length > 1024 * 1024) return false;

            var parameters = stack.PopByteArray().Select(p => (ContractParameterType)p).ToArray();
            if (parameters.Length > 252) return false;

            var returnType = (ContractParameterType)(byte)stack.PopBigInteger();
            var metadata = (ContractMetadata)(byte)stack.PopBigInteger();

            if (stack.PeekByteArray().Length > 252) return false;
            var name = Encoding.UTF8.GetString(stack.PopByteArray());

            if (stack.PeekByteArray().Length > 252) return false;
            var version = Encoding.UTF8.GetString(stack.PopByteArray());

            if (stack.PeekByteArray().Length > 252) return false;
            var author = Encoding.UTF8.GetString(stack.PopByteArray());

            if (stack.PeekByteArray().Length > 252) return false;
            var email = Encoding.UTF8.GetString(stack.PopByteArray());

            if (stack.PeekByteArray().Length > 65536) return false;
            var description = Encoding.UTF8.GetString(stack.PopByteArray());

            var scriptHash = script.ToScriptHash();
            var contract = _contracts.TryGet(scriptHash);
            if (contract == null)
            {
                contract = new Contract
                {
                    Code = new Code
                    {
                        Script = script,
                        ScriptHash = scriptHash,
                        Parameters = parameters,
                        ReturnType = returnType,
                        Metadata = metadata
                    },
                    Name = name,
                    Version = version,
                    Author = author,
                    Email = email,
                    Description = description
                };

                _contracts.Add(scriptHash, contract);
                // TODO: get script hash from engine
                //_contractsCreated.Add(scriptHash, new UInt160(engine.CurrentContext.ScriptHash));
            }

            stack.PushObject(contract);

            return true;
        }

        private bool Contract_Migrate(Stack stack)
        {
            var script = stack.PopByteArray();
            if (script.Length > 1024 * 1024) return false;

            var parameters = stack.PopByteArray().Select(p => (ContractParameterType)p).ToArray();
            if (parameters.Length > 252) return false;

            var returnType = (ContractParameterType)(byte)stack.PopBigInteger();
            var metadata = (ContractMetadata)(byte)stack.PopBigInteger();

            if (stack.PeekByteArray().Length > 252) return false;
            var name = Encoding.UTF8.GetString(stack.PopByteArray());

            if (stack.PeekByteArray().Length > 252) return false;
            var version = Encoding.UTF8.GetString(stack.PopByteArray());

            if (stack.PeekByteArray().Length > 252) return false;
            var author = Encoding.UTF8.GetString(stack.PopByteArray());

            if (stack.PeekByteArray().Length > 252) return false;
            var email = Encoding.UTF8.GetString(stack.PopByteArray());

            if (stack.PeekByteArray().Length > 65536) return false;
            var description = Encoding.UTF8.GetString(stack.PopByteArray());

            var scriptHash = script.ToScriptHash();
            var contract = _contracts.TryGet(scriptHash);
            if (contract == null)
            {
                contract = new Contract
                {
                    Code = new Code
                    {
                        Script = script,
                        ScriptHash = scriptHash,
                        Parameters = parameters,
                        ReturnType = returnType,
                        Metadata = metadata
                    },
                    Name = name,
                    Version = version,
                    Author = author,
                    Email = email,
                    Description = description
                };

                _contracts.Add(scriptHash, contract);
                // TODO: get script hash from engine
                //_contractsCreated.Add(scriptHash, new UInt160(engine.CurrentContext.ScriptHash));
                //if (contract.HasStorage)
                //{
                //    foreach (var pair in _storages.Find(engine.CurrentContext.ScriptHash).ToArray())
                //    {
                //        _storages.Add(new StorageKey
                //        {
                //            ScriptHash = scriptHash,
                //            Key = pair.Key.Key
                //        }, new StorageValue
                //        {
                //            Value = pair.Value.Value
                //        });
                //    }
                //}
            }

            stack.PushObject(contract);

            return Contract_Destroy(stack);
        }

        private bool Contract_GetStorageContext(Stack stack)
        {
            var contract = stack.PopObject<Contract>();

            if (!_contractsCreated.TryGetValue(contract.ScriptHash, out var created)) return false;
            // TODO: get script hash from engine
            // if (!created.Equals(new UInt160(engine.CurrentContext.ScriptHash))) return false;

            stack.PushObject(new StorageContext
            {
                ScriptHash = contract.ScriptHash,
                IsReadOnly = false
            });

            return true;
        }

        private bool Contract_Destroy(Stack engine)
        {
            // TODO: get script hash from engine
            //var hash = new UInt160(engine.CurrentContext.ScriptHash);
            //var contract = _contracts.TryGet(hash);
            //if (contract == null) return true;
            //_contracts.Delete(hash);
            //if (contract.HasStorage)
            //    foreach (var pair in _storages.Find(hash.ToArray()))
            //        _storages.Delete(pair.Key);
            return true;
        }

        private bool Storage_Put(Stack stack)
        {
            var context = stack.PopObject<StorageContext>();
            if (context == null) return false;
            if (context.IsReadOnly) return false;
            if (!CheckStorageContext(context)) return false;

            var key = stack.PopByteArray();
            if (key.Length > 1024) return false;

            var value = stack.PopByteArray();

            _storages.GetAndChange(new StorageKey
            {
                ScriptHash = context.ScriptHash,
                Key = key
            }, () => new StorageValue()).Value = value;

            return true;
        }

        private bool Storage_Delete(Stack stack)
        {
            var context = stack.PopObject<StorageContext>();
            if (context == null) return false;
            if (context.IsReadOnly) return false;
            if (!CheckStorageContext(context)) return false;

            var key = stack.PopByteArray();

            _storages.Delete(new StorageKey
            {
                ScriptHash = context.ScriptHash,
                Key = key
            });

            return true;
        }
    }
}