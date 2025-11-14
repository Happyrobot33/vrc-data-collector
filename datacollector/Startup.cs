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

        //read in admin token
        string? adminToken = Environment.GetEnvironmentVariable("INFLUXDB2_ADMIN_TOKEN");

        if (string.IsNullOrEmpty(adminToken))
        {
            Console.WriteLine("INFLUXDB2_ADMIN_TOKEN environment variable not set. Exiting.");
            return;
        }

        var influxService = new InfluxDBService(adminToken);

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

        if (string.IsNullOrEmpty(adminToken))
        {
            Console.WriteLine("USERNAME_HASH_SECRET environment variable not set. Exiting.");
            return;
        }

        //read the secret from the file
        using HMACSHA512 hmac = new HMACSHA512(Encoding.UTF8.GetBytes(adminToken));

        string? machineName = Environment.GetEnvironmentVariable("MACHINE_NAME");
        //this cant be empty, exit
        if (string.IsNullOrEmpty(machineName))
        {
            Console.WriteLine("MACHINE_NAME environment variable not set. Exiting.");
            return;
        }

        ConcurrentDictionary<string, ConcurrentQueue<PointData>> points = new ConcurrentDictionary<string, ConcurrentQueue<PointData>>();

        //initialize the dictionarys
        const string systemInfoBucket = "system_monitoring";

        //spin up threads to flush data
        for (int i = 0; i < 10; i++)
        {
            _ = Task.Run(() => FlushData(points, influxService));
        }


        //print out how many points are waiting to be in flight
        _ = Task.Run(async () =>
        {
            while (true)
            {
                //get all in que
                int totalPoints = 0;
                foreach (var queue in points.Values)
                {
                    totalPoints += queue.Count;
                }

                //only do this every second using utc time
                if (DateTime.UtcNow.Millisecond % 1000 == 0)
                {
                    //Console.WriteLine($"Points in queue: {totalPoints}");
                }

                //write system info point
                var sysPoint = PointData.Measurement("points_in_queue")
                    .Tag("machine", machineName)
                    .Field("value", totalPoints)
                    .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

                QuePoint(points, systemInfoBucket, sysPoint);

                //throttle this since this has no technical speed limit
                await Task.Delay(100);
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

                    var targetBucket = data.worldid;

                    //add some general data to system info
                    QuePoint(points, systemInfoBucket, PointData.Measurement("players_in_world")
                            .Tag("machine", machineName)
                            .Field("value", data.totalplayers)
                            .Timestamp(time, WritePrecision.Ns));
                    QuePoint(points, systemInfoBucket, PointData.Measurement("players_collected")
                            .Tag("machine", machineName)
                            .Field("value", data.playerdatacollected.Count)
                            .Timestamp(time, WritePrecision.Ns));

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

                        QuePoint(points, targetBucket, PointData.Measurement("fps")
                            .Tag("user", playername)
                            .Field("value", player.fps)
                            .Timestamp(time, WritePrecision.Ns));
                        QuePoint(points, targetBucket, PointData.Measurement("rotation")
                            .Tag("user", playername)
                            .Field("value", player.rotation)
                            .Timestamp(time, WritePrecision.Ns));
                        QuePoint(points, targetBucket, PointData.Measurement("size")
                            .Tag("user", playername)
                            .Field("value", player.size)
                            .Timestamp(time, WritePrecision.Ns));
                        QuePoint(points, targetBucket, PointData.Measurement("vr")
                            .Tag("user", playername)
                            .Field("value", player.vr)
                            .Timestamp(time, WritePrecision.Ns));
                        QuePoint(points, targetBucket, EncodePosition("position", playername, player.position, time));
                        /* points.Enqueue(EncodePosition("left_eye_position", playername, player.lefteyeposition, time));
                        points.Enqueue(EncodeQuaternion("left_eye_rotation", playername, player.lefteyerotation, time));
                        points.Enqueue(EncodePosition("right_eye_position", playername, player.righteyeposition, time));
                        points.Enqueue(EncodeQuaternion("right_eye_rotation", playername, player.righteyerotation, time)); */
                        QuePoint(points, targetBucket, EncodePosition("head_position", playername, player.headposition, time));
                        QuePoint(points, targetBucket, EncodeQuaternion("head_rotation", playername, player.headrotation, time));
                        QuePoint(points, targetBucket, EncodePosition("right_hand_position", playername, player.righthandposition, time));
                        QuePoint(points, targetBucket, EncodeQuaternion("right_hand_rotation", playername, player.righthandrotation, time));
                        QuePoint(points, targetBucket, EncodePosition("left_hand_position", playername, player.lefthandposition, time));
                        QuePoint(points, targetBucket, EncodeQuaternion("left_hand_rotation", playername, player.lefthandrotation, time));

                        //Console.WriteLine($"Wrote data for player {playername}");
                    }
                }
            }
        }
    }

    private static void QuePoint(ConcurrentDictionary<string, ConcurrentQueue<PointData>> points, string bucketName, PointData point)
    {
        points.AddOrUpdate(bucketName,
                                new ConcurrentQueue<PointData>(new[] { point }),
                                (key, existingQueue) =>
                                {
                                    existingQueue.Enqueue(point);
                                    return existingQueue;
                                });
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

    public static async Task FlushData(ConcurrentDictionary<string, ConcurrentQueue<PointData>> points, InfluxDBService influxService)
    {
        Dictionary<string, bool> bucketExistsDict = new Dictionary<string, bool>();

        while (true)
        {
            //get a list of keys
            foreach (var key in points.Keys)
            {
                //get the point list
                var pointList = points.GetOrAdd(key, new ConcurrentQueue<PointData>());

                //make sure target bucket is set
                if (string.IsNullOrEmpty(key))
                {
                    await Task.Delay(100);
                    continue;
                }

                //only flush when we clear a threshold of points
                if (pointList.Count < 1000)
                {
                    continue;
                }

                //collect into a list
                List<PointData> pointsToWrite = new List<PointData>();
                while (pointList.TryDequeue(out var point))
                {
                    pointsToWrite.Add(point);
                }


                if (!bucketExistsDict.ContainsKey(key))
                {
                    bucketExistsDict[key] = false;
                }

                //ensure bucket
                if (bucketExistsDict[key] == false)
                {
                    var exists = await influxService.EnsureBucketAsync(key, "org");
                    bucketExistsDict[key] = exists;
                }

                influxService.Write(write =>
                {
                    //Console.WriteLine($"Point threshold reached, Writing {pointsToWrite.Count} points to bucket {targetBucket}...");
                    try
                    {

                        write.WritePoints(pointsToWrite, key, "org");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error writing points: {ex.Message}");
                    }
                });
                //Console.WriteLine($"Flush complete");
            }
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
