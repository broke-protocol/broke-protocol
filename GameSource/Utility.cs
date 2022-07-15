using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using UnityEngine;
using System;


namespace BrokeProtocol.GameSource
{
    public class CrimeSave
    {
        public CrimeSave() { }
        public CrimeSave(byte index, int[] wearables, float timeSinceLast, ShPlayer witness)
        {
            Index = index;
            Wearables = wearables;
            TimeSinceLast = timeSinceLast;

            if (witness)
            {
                if (witness.isHuman)
                {
                    WitnessPlayerAccount = witness.username;
                }
                else
                {
                    WitnessBotID = witness.ID;
                }
            }
        }

        public byte Index { get; set; }
        public int[] Wearables { get; set; }
        public float TimeSinceLast { get; set; }
        public string WitnessPlayerAccount { get; set; } = string.Empty;
        public int WitnessBotID { get; set; }
    }

    public static class CrimeIndex
    {
        public const byte Null = 0;
        public const byte Obstruction = 1;
        public const byte Intoxication = 2;
        public const byte Intimidation = 3;
        public const byte FalseArrest = 4;
        public const byte Contraband = 5;
        public const byte AutoTheft = 6;
        public const byte Theft = 7;
        public const byte Assault = 8;
        public const byte PrisonBreak = 9;
        public const byte ArmedAssault = 10;
        public const byte Bombing = 11;
        public const byte Robbery = 12;
        public const byte Murder = 13;
        public const byte NoLicense = 14;
        public const byte Trespassing = 15;
        public const byte AnimalCruelty = 16;
        public const byte AnimalKilling = 17;
        public const byte Count = 18;
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
        public byte index;

        public Crime(byte index, string crimeName, bool witness, float repeatDelay, float jailtime)
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
            for (int i = 0; i < wearables.Length; i++)
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
    }

    public static class Utility
    {
        public static LimitQueue<ShPlayer> chatted = new LimitQueue<ShPlayer>(8, 20f);

        public static LimitQueue<ShPlayer> trySell = new LimitQueue<ShPlayer>(0, 5f);

        public static LimitQueue<string> tryRegister = new LimitQueue<string>(0, 5f);

        public const int maxWantedLevel = 5;
        public const string starName = "Star";

        public static readonly Crime[] crimeTypes = new Crime[]
        {
            new Crime(CrimeIndex.Null,          "Null",             false, 0f, 0f),
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
