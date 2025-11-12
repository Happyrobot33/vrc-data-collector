using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Services;
using System;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

class Program
{
    //main function
    static async Task Main(string[] args)
    {
        //print args 
        foreach (var arg in args)
        {
            Console.WriteLine($"Arg: {arg}");
        }

        var influxService = new InfluxDBService();

        //find the VRC log
        //TODO: This needs to be actively re-queried in case of log rotation
        //if done in a seperate task it might be a PITA due to locking, but should be possible
        VRCLog log = new VRCLog();
        log.findVRCLog();

        //check if this line is relevant using regex
        var logFilter = new Regex(@"([0-9]+(\.[0-9]+)+) [0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,3})? Debug\s+-\s+DATACOLLECTOR_JSON: ({.*})");

        using var reader = new System.IO.StreamReader(log.logStream!);

        //print what user we are
        string user = Environment.UserName;
        Console.WriteLine($"Running as user: {user}");

        //read from env variable
        string? secret = Environment.GetEnvironmentVariable("USERNAME_HASH_SECRET");

        if (string.IsNullOrEmpty(secret))
        {
            Console.WriteLine("USERNAME_HASH_SECRET environment variable not set. Exiting.");
            return;
        }

        //read the secret from the file
        using HMACSHA512 hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));

        string? machineName = Environment.GetEnvironmentVariable("MACHINE_NAME");

        ConcurrentQueue<PointData> points = new ConcurrentQueue<PointData>();

        //spin up threads to flush data
        for (int i = 0; i < 10; i++)
        {
            _ = Task.Run(() => FlushData(points, influxService));
        }

        //print out how many points are waiting to be in flight
        _ = Task.Run(async () =>
        {
            bool bucketExists = false;
            string systemInfoBucket = "system_monitoring";

            if (bucketExists == false)
            {
                //ensure the system monitoring bucket exists
                try
                {
                    await influxService.EnsureBucketAsync(systemInfoBucket, "org");
                }
                catch (InfluxDB.Client.Core.Exceptions.UnprocessableEntityException ex) when (ex.Message.Contains("bucket with name") && ex.Message.Contains("already exists"))
                {
                    //bucket exists
                    bucketExists = true;
                }
            }

            while (true)
            {
                //todo: write this into influxdb itself in a new bucket?
                //maybe make each tag a different machine identifier
                Console.WriteLine($"Points in queue: {points.Count}");

                if (!string.IsNullOrEmpty(machineName))
                {
                    //write system info point
                    var sysPoint = PointData.Measurement("points_in_queue")
                        .Tag("machine", machineName)
                        .Field("value", points.Count)
                        .Timestamp(DateTime.UtcNow, WritePrecision.Ns);
                    
                    influxService.Write(write =>
                    {
                        write.WritePoint(sysPoint, systemInfoBucket, "org");
                    });
                }
                await Task.Delay(1000);
            }
        });

        //print out all the lines
        while (true)
        {
            while (!reader.EndOfStream)
            {
                //we have new lines!
                var line = reader.ReadLine();

                if (line == null) continue;

                var match = logFilter.Match(line);
                //print the groups
                if (match.Success)
                {
                    //Console.WriteLine("Matched");
                    //get capture group 4 (or 5?)
                    var json = match.Groups[4].Value;
                    //Console.WriteLine(json);

                    //interpret the json
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<DataRoot>(json);

                    if (data == null)
                    {
                        Console.WriteLine("Failed to parse JSON");
                        continue;
                    }

                    //convert the time from ticks to nanoseconds
                    var time = new DateTime(data.utctime);

                    targetBucket = data.worldid;

                    //need to find a way to include global data here

                    //foreach player
                    foreach (var player in data.playerdatacollected)
                    {
                        //hash the playername so we arent distributing PII
                        string playername = GetHash(hmac, player.displayname);
                        //if hashing went wrong, throw an exception
                        if (string.IsNullOrEmpty(playername))
                        {
                            throw new Exception("Failed to hash player name");
                        }

                        points.Enqueue(PointData.Measurement("fps")
                            .Tag("user", playername)
                            .Field("value", player.fps)
                            .Timestamp(time, WritePrecision.Ns));
                        points.Enqueue(PointData.Measurement("rotation")
                            .Tag("user", playername)
                            .Field("value", player.rotation)
                            .Timestamp(time, WritePrecision.Ns));
                        points.Enqueue(PointData.Measurement("size")
                            .Tag("user", playername)
                            .Field("value", player.size)
                            .Timestamp(time, WritePrecision.Ns));
                        points.Enqueue(PointData.Measurement("vr")
                            .Tag("user", playername)
                            .Field("value", player.vr)
                            .Timestamp(time, WritePrecision.Ns));
                        points.Enqueue(EncodePosition("position", playername, player.position, time));
                        /* points.Enqueue(EncodePosition("left_eye_position", playername, player.lefteyeposition, time));
                        points.Enqueue(EncodeQuaternion("left_eye_rotation", playername, player.lefteyerotation, time));
                        points.Enqueue(EncodePosition("right_eye_position", playername, player.righteyeposition, time));
                        points.Enqueue(EncodeQuaternion("right_eye_rotation", playername, player.righteyerotation, time)); */
                        points.Enqueue(EncodePosition("head_position", playername, player.headposition, time));
                        points.Enqueue(EncodeQuaternion("head_rotation", playername, player.headrotation, time));
                        points.Enqueue(EncodePosition("right_hand_position", playername, player.righthandposition, time));
                        points.Enqueue(EncodeQuaternion("right_hand_rotation", playername, player.righthandrotation, time));
                        points.Enqueue(EncodePosition("left_hand_position", playername, player.lefthandposition, time));
                        points.Enqueue(EncodeQuaternion("left_hand_rotation", playername, player.lefthandrotation, time));

                        //Console.WriteLine($"Wrote data for player {playername}");
                    }
                }
            }
        }
    }

    private static PointData EncodePosition(string measurement, string userTag, Position position, DateTime time)
    {
        return PointData.Measurement(measurement)
            .Tag("user", userTag)
            .Field("x", position.x)
            .Field("y", position.y)
            .Field("z", position.z)
            .Timestamp(time, WritePrecision.Ns);
    }

    private static PointData EncodeQuaternion(string measurement, string userTag, Quaternion position, DateTime time)
    {
        return PointData.Measurement(measurement)
            .Tag("user", userTag)
            .Field("x", position.x)
            .Field("y", position.y)
            .Field("z", position.z)
            .Field("w", position.w)
            .Timestamp(time, WritePrecision.Ns);
    }

    static string targetBucket = "";

    public static async Task FlushData(ConcurrentQueue<PointData> points, InfluxDBService influxService)
    {
        bool bucketExists = false;

        while (true)
        {
            //make sure target bucket is set
            if (string.IsNullOrEmpty(targetBucket))
            {
                await Task.Delay(1000);
                continue;
            }

            //only flush when we clear a threshold of points
            if (points.Count < 1000)
            {
                await Task.Delay(500);
                continue;
            }

            //collect into a list
            List<PointData> pointsToWrite = new List<PointData>();
            while (points.TryDequeue(out var point))
            {
                pointsToWrite.Add(point);
            }

            //flush to the database

            //ensure bucket
            if (!string.IsNullOrEmpty(targetBucket) && bucketExists == false)
            {
                //catch that the bucket already exists, and set our flag for that
                try
                {
                    await influxService.EnsureBucketAsync(targetBucket, "org");
                }
                catch (InfluxDB.Client.Core.Exceptions.UnprocessableEntityException ex) when (ex.Message.Contains("bucket with name") && ex.Message.Contains("already exists"))
                {
                    //bucket exists
                    bucketExists = true;
                }
            }

            influxService.Write(write =>
            {
                //Console.WriteLine($"Point threshold reached, Writing {pointsToWrite.Count} points to bucket {targetBucket}...");
                try
                {

                    write.WritePoints(pointsToWrite, targetBucket, "org");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing points: {ex.Message}");
                }
            });
            //Console.WriteLine($"Flush complete");
        }
    }

    private static string GetHash(HashAlgorithm hashAlgorithm, string input)
    {

        // Convert the input string to a byte array and compute the hash.
        byte[] data = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(input));

        // Create a new Stringbuilder to collect the bytes
        // and create a string.
        var sBuilder = new StringBuilder();

        // Loop through each byte of the hashed data
        // and format each one as a hexadecimal string.
        for (int i = 0; i < data.Length; i++)
        {
            sBuilder.Append(data[i].ToString("x2"));
        }

        // Return the hexadecimal string.
        return sBuilder.ToString();
    }
}
