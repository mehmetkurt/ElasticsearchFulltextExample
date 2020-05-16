﻿// Copyright (c) Philipp Wagner. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ElasticsearchFulltextExample.Web.Database.Context;
using ElasticsearchFulltextExample.Web.Database.Factory;
using ElasticsearchFulltextExample.Web.Database.Model;
using ElasticsearchFulltextExample.Web.Logging;
using ElasticsearchFulltextExample.Web.Options;
using ElasticsearchFulltextExample.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ElasticsearchFulltextExample.Web.Hosting
{
    public class DocumentIndexerHostedService : BackgroundService
    {
        private readonly IndexerOptions options;

        private readonly ILogger<DocumentIndexerHostedService> logger;
        private readonly ApplicationDbContextFactory applicationDbContextFactory;
        private readonly ElasticsearchIndexService elasticsearchIndexService;

        public DocumentIndexerHostedService(ILogger<DocumentIndexerHostedService> logger, IOptions<IndexerOptions> options, ApplicationDbContextFactory applicationDbContextFactory, ElasticsearchIndexService elasticsearchIndexService)
        {
            this.logger = logger;
            this.options = options.Value;
            this.elasticsearchIndexService = elasticsearchIndexService;
            this.applicationDbContextFactory = applicationDbContextFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var indexDelay = TimeSpan.FromSeconds(options.IndexDelay);

            if (logger.IsDebugEnabled())
            {
                logger.LogDebug($"ElasticsearchIndexHostedService is starting with Index Delay: {options.IndexDelay} seconds.");
            }

            cancellationToken.Register(() => logger.LogDebug($"ElasticsearchIndexHostedService background task is stopping."));

            while (!cancellationToken.IsCancellationRequested)
            {
                if (logger.IsDebugEnabled())
                {
                    logger.LogDebug($"ElasticsearchIndexHostedService task doing background work.");
                }

                await IndexDocumentsAsync(cancellationToken);

                await Task.Delay(indexDelay, cancellationToken);
            }

            logger.LogDebug($"ElasticsearchIndexHostedService background task is stopping.");
        }

        private async Task IndexDocumentsAsync(CancellationToken cancellationToken)
        {
            using(var context = applicationDbContextFactory.Create())
            {
                await IndexScheduledDocuments(context, cancellationToken);
                await RemoveDeletedDocuments(context, cancellationToken);
            }

            async Task RemoveDeletedDocuments(ApplicationDbContext context, CancellationToken cancellationToken)
            {
                var documents = context.Documents
                    .Where(x => x.Status == StatusEnum.ScheduledDelete)
                    .AsNoTracking()
                    .AsAsyncEnumerable();

                await foreach (Document document in documents.WithCancellation(cancellationToken))
                {
                    if (logger.IsInformationEnabled())
                    {
                        logger.LogInformation($"Start indexing Document: {document.DocumentId}");
                    }

                    try
                    {
                        await elasticsearchIndexService.DeleteDocumentAsync(document, cancellationToken);

                        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE documents SET status = {StatusEnum.Deleted}, indexed_at = {null}");
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, $"Indexing Document '{document.Id}' failed");

                        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE documents SET status = {StatusEnum.Failed}, indexed_at = {null}");
                    }

                    if (logger.IsInformationEnabled())
                    {
                        logger.LogInformation($"Finished indexing Document: {document.DocumentId}");
                    }
                }
            }

            async Task IndexScheduledDocuments(ApplicationDbContext context, CancellationToken cancellationToken)
            {
                var documents = context.Documents
                    .Where(x => x.Status == StatusEnum.ScheduledIndex)
                    .AsNoTracking()
                    .AsAsyncEnumerable();

                await foreach (Document document in documents.WithCancellation(cancellationToken))
                {
                    if (logger.IsInformationEnabled())
                    {
                        logger.LogInformation($"Start indexing Document: {document.DocumentId}");
                    }

                    try
                    {
                        await elasticsearchIndexService.IndexDocumentAsync(document, cancellationToken);

                        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE documents SET status = {StatusEnum.Indexed}, indexed_at = {DateTime.UtcNow}");
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, $"Indexing Document '{document.Id}' failed");

                        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE documents SET status = {StatusEnum.Failed}, indexed_at = {null}");
                    }

                    if (logger.IsInformationEnabled())
                    {
                        logger.LogInformation($"Finished indexing Document: {document.DocumentId}");
                    }
                }
            }
        }
    }
}
