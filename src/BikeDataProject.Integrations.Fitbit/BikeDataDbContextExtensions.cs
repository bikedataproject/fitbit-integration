using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BikeDataProject.Integrations.Fitbit.Db;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using TCX.Domain;

namespace BikeDataProject.Integrations.Fitbit
{
    public static class BikeDataDbContextExtensions
    {
        public static async Task<DB.User> CreateOrGetUser(this DB.BikeDataDbContext dbContext, FitbitDbContext db, User fitbitUser,
            string provider = Constants.FitbitProviderName)
        {
            var user = (from u in dbContext.Users
                where u.ProviderUser == fitbitUser.UserId &&
                      u.Provider == provider
                select u).FirstOrDefault();
            if (user == null)
            {
                user = new DB.User()
                {
                    Provider = Constants.FitbitProviderName,
                    ProviderUser = fitbitUser.UserId
                };
                await dbContext.Users.AddAsync(user);
                await dbContext.SaveChangesAsync();
            }

            if (fitbitUser.BikeDataProjectId == null)
            {
                // update the local user with the user id in the contributions db.
                fitbitUser.BikeDataProjectId = user.Id;
                db.Update(user);
                await db.SaveChangesAsync();
            }

            return user;
        }

        public static IEnumerable<DB.Contribution>? ToContributions(this TrainingCenterDatabase tcx)
        {
            if (tcx.Activities?.Activity == null) yield break;
            foreach (var a in tcx.Activities.Activity)
            {
                if (a.Lap == null) continue;

                foreach (var l in a.Lap)
                {
                    if (l.Track == null) continue;

                    var coordinates = new List<Coordinate>();
                    var timestamps = new List<DateTime>();
                    var distance = 0.0;
                    foreach (var t in l.Track)
                    {
                        coordinates.Add(new CoordinateZ(t.Position.LongitudeDegrees, t.Position.LatitudeDegrees, t.AltitudeMeters));
                        timestamps.Add(t.Time);

                        if (coordinates.Count > 1)
                        {
                            distance += Helpers.CalculateDistance(coordinates[^2], coordinates[^1]);
                        }
                    }

                    var first = timestamps.First();
                    var last = timestamps.Last();
                    var geometry = new LineString(coordinates.ToArray());
                    yield return new DB.Contribution()
                    {
                        Distance = (int) distance,
                        Duration = (int) ((last - first).TotalSeconds),
                        AddedOn = DateTime.Now,
                        UserAgent = "fitbit",
                        TimeStampStart = first,
                        TimeStampStop = last,
                        PointsTime = timestamps.ToArray(),
                        PointsGeom = new PostGisWriter().Write(geometry),
                    };
                }
            }
        }

        public static async Task SaveContribution(this DB.BikeDataDbContext dbContext, FitbitDbContext db,
            DB.Contribution contribution, User fitbitUser, long logId)
        {
            await dbContext.Contributions.AddAsync(contribution);
            await dbContext.SaveChangesAsync();

            var fitBitContribution = new Contribution()
            {
                UserId = fitbitUser.Id,
                BikeDataProjectId = contribution.ContributionId,
                FitBitLogId = logId
            };
            await db.Contributions.AddAsync(fitBitContribution);
            await db.SaveChangesAsync();
        }
    }
}