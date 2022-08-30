using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using UnityEngine;


namespace BrokeProtocol.GameSource
{
    [Serializable]
    public struct CrimeSave
    {
        public CrimeSave(CrimeIndex crimeIndex, int[] wearables, float timeSinceLast, ShPlayer witness)
        {
            this.crimeIndex = crimeIndex;
            this.wearables = wearables;
            this.timeSinceLast = timeSinceLast;

            if (witness)
            {
                if (witness.isHuman)
                {
                    witnessPlayerAccount = witness.username;
                    witnessBotID = 0;
                }
                else
                {
                    witnessPlayerAccount = null;
                    witnessBotID = witness.ID;
                }
            }
            else
            {
                witnessPlayerAccount = null;
                witnessBotID = 0;
            }
        }

        public CrimeIndex crimeIndex;
        public int[] wearables;
        public float timeSinceLast;
        public string witnessPlayerAccount;
        public int witnessBotID;
    }

    // Store as a string for future change compatibility
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CrimeIndex
    {
        Obstruction,
        Intoxication,
        Intimidation,
        FalseArrest,
        Contraband,
        AutoTheft,
        Theft,
        Assault,
        PrisonBreak,
        ArmedAssault,
        Bombing,
        Robbery,
        Murder,
        NoLicense,
        Trespassing,
        AnimalCruelty,
        AnimalKilling,
    }

    [Serializable]
    public struct Crime
    {
        public string crimeName;
        public bool witness;
        public float repeatDelay;
        public int fine;
        public int experiencePenalty;
        public float expiration;
        public float jailtime;
        public CrimeIndex index;

        public Crime(CrimeIndex index, string crimeName, bool witness, float repeatDelay, float jailtime)
        {
            this.index = index;
            this.crimeName = crimeName;
            this.witness = witness;
            this.repeatDelay = repeatDelay;
            this.jailtime = jailtime;
            fine = Mathf.CeilToInt(jailtime * 1f);
            experiencePenalty = Mathf.CeilToInt(jailtime * 0.02f);
            expiration = jailtime * 2f;
        }
    }

    [Serializable]
    public sealed class Offense
    {
        public Crime crime;
        public ShPlayer witness;
        public int[] wearables;
        public float commitTime;

        public bool disguised;

        public Offense(Crime crime, ShWearable[] wearables, ShPlayer witness, float relativeCommitTime = 0f)
        {
            this.crime = crime;

            this.wearables = new int[wearables.Length];
            for (var i = 0; i < wearables.Length; i++)
            {
                this.wearables[i] = wearables[i].index;
            }

            this.witness = witness;
            commitTime = Time.time - relativeCommitTime;

            disguised = false;
        }

        public float AdjustedExpiration
        {
            get
            {
                var expiration = crime.expiration;

                if (disguised) expiration *= 0.5f;

                if (crime.witness && !witness) expiration *= 0.5f;

                return expiration;
            }
        }

        public float ElapsedTime => Time.time - commitTime;

        public float TimeLeft => commitTime + AdjustedExpiration - Time.time;
    }

    public static class LifeUtility
    {
        public static LimitQueue<ShPlayer> trySell = new LimitQueue<ShPlayer>(0, 5f);

        public const int maxWantedLevel = 5;
        public const string starName = "Star";

        public static readonly Crime[] crimeTypes = new Crime[]
        {
            new Crime(CrimeIndex.Obstruction,   "Obstruction",      false, 0f, 20f),
            new Crime(CrimeIndex.Intoxication,  "Intoxication",     false, 60f, 30f),
            new Crime(CrimeIndex.Intimidation,  "Intimidation",     true, 10f, 35f),
            new Crime(CrimeIndex.FalseArrest,   "False Arrest",     true, 60f, 40f),
            new Crime(CrimeIndex.Contraband,    "Contraband",       false, 120f, 60f),
            new Crime(CrimeIndex.AutoTheft,     "Auto Theft",       true, 0f, 100f),
            new Crime(CrimeIndex.Theft,         "Theft",            false, 5f, 120f),
            new Crime(CrimeIndex.Assault,       "Assault",          true, 10f, 140f),
            new Crime(CrimeIndex.PrisonBreak,   "Prison Break",     false, 0f, 160f),
            new Crime(CrimeIndex.ArmedAssault,  "Armed Assault",    true, 10f, 180f),
            new Crime(CrimeIndex.Bombing,       "Bombing",          false, 240f, 200f),
            new Crime(CrimeIndex.Robbery,       "Robbery",          false, 240f, 220f),
            new Crime(CrimeIndex.Murder,        "Murder",           true, 0f, 240f),
            new Crime(CrimeIndex.NoLicense,     "No license",       true, 30f, 15f),
            new Crime(CrimeIndex.Trespassing,   "Trespassing",      false, 240f, 380f),
            new Crime(CrimeIndex.AnimalCruelty, "Animal Cruelty",   true, 10f, 70f),
            new Crime(CrimeIndex.AnimalKilling, "Animal Killing",   true, 0f, 120f),
        };
    }
}
