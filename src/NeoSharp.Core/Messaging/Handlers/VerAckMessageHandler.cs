﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NeoSharp.Core.Blockchain;
using NeoSharp.Core.Messaging.Messages;
using NeoSharp.Core.Network;

namespace NeoSharp.Core.Messaging.Handlers
{
    public class VerAckMessageHandler : IMessageHandler<VerAckMessage>
    {
        private readonly IBlockchain _blockchain;
        private readonly ILogger<VerAckMessageHandler> _logger;

        public VerAckMessageHandler(IBlockchain blockchain, ILogger<VerAckMessageHandler> logger)
        {
            _blockchain = blockchain ?? throw new ArgumentNullException(nameof(blockchain));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Handle(VerAckMessage message, IPeer sender)
        {
            sender.IsReady = true;

            if (_blockchain.BlockHeaderHeight < sender.Version.StartHeight)
            {
                _logger.LogInformation($"The peer start height is {sender.Version.StartHeight} but the current start height is {_blockchain.BlockHeaderHeight}");
                await sender.Send(new GetBlockHeadersMessage(_blockchain.CurrentBlockHeaderHash));
            }
        }
    }
}