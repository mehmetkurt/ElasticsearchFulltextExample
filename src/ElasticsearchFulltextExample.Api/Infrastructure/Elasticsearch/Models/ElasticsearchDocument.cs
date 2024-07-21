﻿// Copyright (c) Philipp Wagner. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ElasticsearchFulltextExample.Shared.Constants;
using System.Text.Json.Serialization;

namespace ElasticsearchFulltextExample.Api.Infrastructure.Elasticsearch.Models
{
    public class ElasticsearchDocument
    {
        /// <summary>
        /// A unique document id.
        /// </summary>
        [JsonPropertyName(ElasticConstants.DocumentNames.Id)]
        public required string Id { get; set; }

        /// <summary>
        /// The Title of the Document for Suggestion.
        /// </summary>
        [JsonPropertyName(ElasticConstants.DocumentNames.Title)]
        public required string Title { get; set; }

        /// <summary>
        /// The Original Filename of the uploaded document.
        /// </summary>
        [JsonPropertyName(ElasticConstants.DocumentNames.Filename)]
        public required string Filename { get; set; }

        /// <summary>
        /// The Data of the Document.
        /// </summary>
        [JsonPropertyName(ElasticConstants.DocumentNames.Data)]
        public byte[]? Data { get; set; }

        /// <summary>
        /// Keywords to filter for.
        /// </summary>
        [JsonPropertyName(ElasticConstants.DocumentNames.Keywords)]
        public required string[] Keywords { get; set; }

        /// <summary>
        /// Suggestions for the Autocomplete Field.
        /// </summary>
        [JsonPropertyName(ElasticConstants.DocumentNames.Suggestions)]
        public required string[] Suggestions { get; set; }

        /// <summary>
        /// The Date the document was indexed on.
        /// </summary>
        [JsonPropertyName(ElasticConstants.DocumentNames.IndexedOn)]
        public DateTime? IndexedOn { get; set; }

        /// <summary>
        /// The Attachment generated by Elasticsearch.
        /// </summary>
        [JsonPropertyName(ElasticConstants.DocumentNames.Attachment)]
        public ElasticsearchAttachment? Attachment { get; set; }
    }
}
