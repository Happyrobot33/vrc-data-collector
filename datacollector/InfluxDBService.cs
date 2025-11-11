using System;
using System.Threading.Tasks;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Core.Exceptions;

namespace Services
{
    public class InfluxDBService
    {
        private readonly string _token;

        public InfluxDBService()
        {
            _token = "admin-token";
        }

        public void Write(Action<WriteApi> action)
        {
            using var client = InfluxDBClientFactory.Create("http://influxdb2:8086", _token);
            using var write = client.GetWriteApi();
            action(write);
        }

        public async Task<T> QueryAsync<T>(Func<QueryApi, Task<T>> action)
        {
            using var client = InfluxDBClientFactory.Create("http://influxdb2:8086", _token);
            var query = client.GetQueryApi();
            return await action(query);
        }

        public async Task EnsureBucketAsync(string bucketName, string org)
        {
            Console.WriteLine($"Ensuring bucket '{bucketName}' exists...");
            //get the ID of the org
            using var client = InfluxDBClientFactory.Create("http://influxdb2:8086", _token);
            var orgs = await client.GetOrganizationsApi().FindOrganizationsAsync();
            var organization = orgs.Find(o => o.Name == org);
            if (organization == null)
            {
                Console.WriteLine($"Organization '{org}' not found.");
                return;
            }
            // Prepare the bucket object
            var bucket = new Bucket(name: bucketName, orgID: organization.Id, retentionRules: new List<BucketRetentionRules>());

            // Create the bucket
            var createdBucket = await client.GetBucketsApi().CreateBucketAsync(bucket);
            Console.WriteLine($"Bucket '{createdBucket.Name}' created successfully with ID: {createdBucket.Id}");
        }
    }
}
