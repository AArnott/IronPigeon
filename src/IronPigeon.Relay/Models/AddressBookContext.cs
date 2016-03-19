// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.WindowsAzure.Storage.Table;
    using Microsoft.WindowsAzure.Storage.Table.DataServices;
    using Microsoft.WindowsAzure.StorageClient;
    using Validation;
    using ReadOnlyListOfAddressBookEmailEntity = System.Collections.Generic.IReadOnlyList<AddressBookEmailEntity>;

    public class AddressBookContext : TableServiceContext
    {
        public AddressBookContext(CloudTableClient client, string primaryTableName, string emailAddressTableName)
            : base(client)
        {
            Requires.NotNullOrEmpty(primaryTableName, "primaryTableName");
            Requires.NotNullOrEmpty(emailAddressTableName, "emailAddressTableName");

            this.PrimaryTableName = primaryTableName;
            this.EmailAddressTableName = emailAddressTableName;
        }

        public string PrimaryTableName { get; private set; }

        public string EmailAddressTableName { get; private set; }

        public async Task<AddressBookEntity> GetAsync(string provider, string userId)
        {
            Requires.NotNullOrEmpty(provider, "provider");
            Requires.NotNullOrEmpty(userId, "userId");

            var query = (from inbox in this.CreateQuery<AddressBookEntity>(this.PrimaryTableName)
                         where inbox.RowKey == AddressBookEntity.ConstructRowKey(provider, userId)
                         select inbox).AsTableServiceQuery(this);
            var result = await query.ExecuteSegmentedAsync();
            return result.FirstOrDefault();
        }

        public async Task<AddressBookEmailEntity> GetAddressBookEmailEntityAsync(string email)
        {
            var query = (from address in this.CreateQuery<AddressBookEmailEntity>(this.EmailAddressTableName)
                         where address.RowKey == email.ToLowerInvariant()
                         select address).AsTableServiceQuery(this);
            var result = await query.ExecuteSegmentedAsync();
            return result.FirstOrDefault();
        }

        public async Task<AddressBookEmailEntity> GetAddressBookEmailEntityByHashAsync(string emailHash)
        {
            var query = (from address in this.CreateQuery<AddressBookEmailEntity>(this.EmailAddressTableName)
                         where address.MicrosoftEmailHash == emailHash
                         select address).AsTableServiceQuery(this);
            var result = await query.ExecuteSegmentedAsync();
            return result.FirstOrDefault();
        }

        public async Task<AddressBookEntity> GetAddressBookEntityByEmailHashAsync(string emailHash)
        {
            Requires.NotNull(emailHash, "emailHash");

            var emailEntity = await this.GetAddressBookEmailEntityByHashAsync(emailHash);
            if (emailEntity == null)
            {
                return null;
            }

            var query = (from inbox in this.CreateQuery<AddressBookEntity>(this.PrimaryTableName)
                         where inbox.RowKey == emailEntity.AddressBookEntityRowKey
                         select inbox).AsTableServiceQuery(this);
            var result = await query.ExecuteSegmentedAsync();
            var entryEntity = result.FirstOrDefault();
            return entryEntity;
        }

        public async Task<AddressBookEntity> GetAddressBookEntityByEmailAsync(string email)
        {
            Requires.NotNull(email, "email");

            var emailEntity = await this.GetAddressBookEmailEntityAsync(email);
            if (emailEntity == null)
            {
                return null;
            }

            var query = (from inbox in this.CreateQuery<AddressBookEntity>(this.PrimaryTableName)
                         where inbox.RowKey == emailEntity.AddressBookEntityRowKey
                         select inbox).AsTableServiceQuery(this);
            var result = await query.ExecuteSegmentedAsync();
            var entryEntity = result.FirstOrDefault();
            return entryEntity;
        }

        public async Task<ReadOnlyListOfAddressBookEmailEntity> GetEmailAddressesAsync(AddressBookEntity entity)
        {
            var query = (from address in this.CreateQuery<AddressBookEmailEntity>(this.EmailAddressTableName)
                         where address.AddressBookEntityRowKey == entity.RowKey
                         select address).AsTableServiceQuery(this);
            var result = await query.ExecuteSegmentedAsync();

            return result.ToList();
        }

        public void AddObject(AddressBookEntity entity)
        {
            this.AddObject(this.PrimaryTableName, entity);
        }

        public void AddObject(AddressBookEmailEntity entity)
        {
            this.AddObject(this.EmailAddressTableName, entity);
        }
    }
}