﻿// Copyright (c) Philipp Wagner. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Elasticsearch.Net;
using ElasticsearchFulltextExample.Web.Elasticsearch.Model;
using Nest;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ElasticsearchFulltextExample.Web.Elasticsearch
{
    public class ElasticsearchClient
    {
        public readonly IElasticClient Client;
        public readonly string IndexName;

        public ElasticsearchClient(IElasticClient client, string indexName)
        {
            IndexName = indexName;
            Client = client;
        }

        public ElasticsearchClient(Uri uri, string indexName)
            : this(CreateClient(uri), indexName)
        {
        }

        public Task<CreateIndexResponse> CreateIndexAsync()
        {
            var response = Client.Indices.Exists(IndexName);

            if (response.Exists)
            {
                return null;
            }

            return Client.Indices.CreateAsync(IndexName, descriptor =>
            {
                return descriptor.Map<Document>(mapping => ConfigureDocumentMapping(mapping));
            });
        }

        private ITypeMapping ConfigureDocumentMapping(TypeMappingDescriptor<Document> mapping)
        {
            return mapping.Properties(properties => ConfigureDocumentProperties(properties));
        }

        private IPromise<IProperties> ConfigureDocumentProperties(PropertiesDescriptor<Document> properties)
        {
            return properties
                .Text(textField => textField.Name(document => document.Id))
                .Text(textField => textField.Name(document => document.Title))
                .Text(textField => textField.Name(document => document.Content))
                .Object<Attachment>(attachment => attachment
                    .Name(document => document.Attachment)
                        .Properties(attachmentProperties => attachmentProperties
                            .Text(t => t.Name(n => n.Name))
                            .Text(t => t.Name(n => n.Content))
                            .Text(t => t.Name(n => n.ContentType))
                            .Number(n => n.Name(nn => nn.ContentLength))
                            .Date(d => d.Name(n => n.Date))
                            .Text(t => t.Name(n => n.Author))
                            .Text(t => t.Name(n => n.Title))
                            .Text(t => t.Name(n => n.Keywords))));
        }

        public Task<PutPipelineResponse> CreatePipelineAsync()
        {
            return Client.Ingest.PutPipelineAsync("attachments", p => p
                .Description("Document attachment pipeline")
                .Processors(pr => pr
                .Attachment<Document>(a => a
                    .Field(f => f.Content)
                    .TargetField(f => f.Attachment))));
        }

        public Task<BulkResponse> BulkIndexAsync(IEnumerable<Document> documents)
        {
            var request = new BulkDescriptor();

            foreach (var document in documents)
            {
                request.Index<Document>(op => op
                    .Id(document.Id)
                    .Index(IndexName)
                    .Document(document)
                    .Pipeline("attachments"));
            }

            return Client.BulkAsync(request);
        }

        public Task<IndexResponse> IndexAsync(Document document)
        {
            return Client.IndexAsync(document, x => x
                .Id(document.Id)
                .Index(IndexName)
                .Pipeline("attachments"));
        }

        public Task<ISearchResponse<Document>> SearchAsync(string query)
        {
            return Client.SearchAsync<Document>(document => document
                // Query this Index:
                .Index(IndexName)
                // Highlight Text Content:
                .Highlight(highlight => highlight
                    .Fields(fields => fields.Field(x => x.Attachment.Content)))
                // Now kick off the query:
                .Query(q => BuildQueryContainer(query)));
        }

        public Task<ISearchResponse<Document>> SuggestAsync(string query)
        {
            return Client.SearchAsync<Document>(x => x
                // Query this Index:
                .Index(IndexName)
                // Suggest Titles:
                .Suggest(s => s.Term("suggestion", t => t
                    .Field(x => x.Attachment.Content)
                    .Size(5)
                    .Text(query))));
        }

        private QueryContainer BuildQueryContainer(string query)
        {
            return Query<Document>.MultiMatch(x => x.Query(query)
                .Type(TextQueryType.BestFields)
                .Fields(f => f
                    .Field(x => x.Attachment.Title, 2)
                    .Field(x => x.Attachment.Content, 1)));
        }

        private static IElasticClient CreateClient(Uri uri)
        {
            var connectionPool = new SingleNodeConnectionPool(uri);
            var connectionSettings = new ConnectionSettings(connectionPool);

            return new ElasticClient(connectionSettings);
        }
    }
}