using UnityEngine;
using System.Data;
using Mono.Data.Sqlite;
using System.IO;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using System.Text;
using System.Globalization;
using System.Collections;

public class DataHandler : IBasePage, ISavableComponent
{
    public class TrickEntry
    {
        public enum Status
        {
            Default,
            Landed,
            Banned,
            MaxStatusValues,
        }

        public int index;
        public string name, secondaryName;
        public Status status;
        public int lands;
        public string category;
        public int difficulty;
        public int originalDifficulty;
        public uint hash;
        public bool canBeRolled;
    }

    public class TrickList : ReadOnlyDictionary<string, Dictionary<int, List<TrickEntry>>>, IEnumerable<TrickEntry>
    {
        public TrickList( Dictionary<string, Dictionary<int, List<TrickEntry>>> copy )
            : base( copy )
        {
        }

        public new IEnumerator<TrickEntry> GetEnumerator()
        {
            var enumerator = base.GetEnumerator();
            while( enumerator.MoveNext() )
                foreach( var (difficulty, trickList) in enumerator.Current.Value )
                    foreach( var trick in trickList )
                        yield return trick;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    [HideInInspector] public Dictionary<string, Dictionary<int, List<TrickEntry>>> _trickData = new Dictionary<string, Dictionary<int, List<TrickEntry>>>();
    public TrickList TrickData
    {
        get { return new TrickList( _trickData ); }
    }

    [HideInInspector] Dictionary<uint, TrickEntry> trickDataHashMap = new Dictionary<uint, TrickEntry>();

    [HideInInspector] List<string> _categories = new List<string>();
    public ReadOnlyCollection<string> Categories
    {
        get { return _categories.AsReadOnly(); }
    }

    [HideInInspector] Dictionary<string, string> _categoryDisplayNames = new Dictionary<string, string>();
    public ReadOnlyDictionary<string, string> CategoryDisplayNames
    {
        get { return new ReadOnlyDictionary<string, string >( _categoryDisplayNames ); }
    }

    [HideInInspector] Dictionary<int, string> _difficultyNames = new Dictionary<int, string>();
    public ReadOnlyDictionary<int, string> DifficultyNames
    {
        get { return new ReadOnlyDictionary<int, string>( _difficultyNames ); }
    }

    [HideInInspector] List<Pair<string,string>> _shortTrickNameReplacements = new List<Pair<string, string>>();
    public ReadOnlyCollection<Pair<string, string>> ShortTrickNameReplacements
    {
        get { return _shortTrickNameReplacements.AsReadOnly(); }
    }

    public class ChallengeData
    {
        public string name;
        public string person;
        public ReadOnlyCollection<TrickEntry> tricks;
        public BitArray landedData;
        public int difficulty;
        public string category;
        public int index;
        public uint hash;

        public bool Completed
        {
            get { return landedData.CountBits() == tricks.Count; }
        }
    }

    [HideInInspector] Dictionary<uint, List<ChallengeData>> _challengesData = new Dictionary<uint, List<ChallengeData>>();
    public ReadOnlyDictionary<uint, ReadOnlyCollection<ChallengeData>> ChallengesData
    {
        get
        {
            return new ReadOnlyDictionary<uint, ReadOnlyCollection<ChallengeData>>( 
                _challengesData.ToDictionary( k => k.Key, v => v.Value.AsReadOnly() ) );
        }
    }

    private string databasePath;

    static DataHandler _Instance;
    static public DataHandler Instance { get => _Instance; private set { } }

    private void Awake()
    {
        _Instance = this;
        SaveGameSystem.AddSaveableComponent( this );
        Utility.FunctionTimer.CreateTimer( 0.01f, Initialise );
    }

    private void OnApplicationPause( bool paused )
    {
        if( paused )
            Save( true );
    }

    private void OnApplicationFocus( bool hasFocus )
    {
        if( !hasFocus )
            Save( true );
    }

    private const string saveDataName = "Data";
    private const string databaseName = "Database.db";

    private void Initialise()
    {
        Debug.Log( "DataHandler::Initialise" );

        // Copy db file to local device persistent storage (if not already there)
        InitSqliteFile( databaseName );

        // Open database
        string connection = "URI=file:" + databasePath;
        using SqliteConnection dbcon = new SqliteConnection( connection );
        dbcon.Open();

        Debug.Log( "DataHandler::Initialise - Load Categories" );

        // Load categories
        using( var dbcmd = dbcon.CreateCommand() )
        {
            dbcmd.CommandText = "SELECT * from Categories";
            using var reader = dbcmd.ExecuteReader();

            while( reader.Read() )
            {
                _categories.Add( reader.GetString( 0 ) );
                _categoryDisplayNames.Add( _categories.Back(), reader.GetString( 1 ) );
            }
        }

        Debug.Log( "DataHandler::Initialise - Load Difficulty name" );

        // Load difficulty names
        using( var dbcmd = dbcon.CreateCommand() )
        {
            dbcmd.CommandText = "SELECT * from DifficultyNames";
            using var reader = dbcmd.ExecuteReader();

            while( reader.Read() )
                _difficultyNames.Add( reader.GetInt32( 0 ), reader.GetString( 1 ) );
        }

        Debug.Log( "DataHandler::Initialise - Load Tricks" );

        // Load all tricks
        using( var dbcmd = dbcon.CreateCommand() )
        {
            dbcmd.CommandText = "SELECT * from Tricks";
            using var reader = dbcmd.ExecuteReader();

            // Setup empty data structures
            foreach( var category in Categories )
            {
                var categoryData = new Dictionary< int, List< TrickEntry >>();
                foreach( var (difficulty, _) in DifficultyNames )
                    categoryData.Add( difficulty, new List<TrickEntry>() );
                _trickData.Add( category, categoryData );
            }

            var myTI = new CultureInfo( "en-US", false ).TextInfo;

            // Read in the tricks and populate the data
            while( reader.Read() )
            {
                var name = reader.GetStringSafe( 0 );
                var secondaryName = reader.GetStringSafe( 1 );
                var categories = reader.GetStringSafe( 2 );

                List<Pair<int, string>> difficultyMap = new List<Pair<int, string>>
                {
                    new Pair<int, string>( reader.GetInt32Safe( 3 ), "" ),
                    new Pair<int, string>( reader.GetInt32Safe( 4 ), "Fakie " ),
                    new Pair<int, string>( reader.GetInt32Safe( 5 ), "Switch " ),
                    new Pair<int, string>( reader.GetInt32Safe( 6 ), "Nollie " ),
                };

                bool canBeRolled = !reader.GetBoolean( 7 );

                foreach( var c in categories.Split( ',' ) )
                {
                    var category = c.Trim();

                    if( !categories.Contains( category ) )
                        Debug.LogError( name + " row from SQL database contains an invalid category: " + category );

                    foreach( var (difficulty, prefix) in difficultyMap )
                    {
                        if( difficulty <= 0 )
                            continue;

                        var hash = xxHashSharp.xxHash.CalculateHash( Encoding.ASCII.GetBytes( category + prefix + name ) );
                        var index = _trickData.Count;

                        var newEntry = new TrickEntry()
                        {
                            index = index,
                            name = myTI.ToTitleCase( prefix + name ),
                            secondaryName = myTI.ToTitleCase( secondaryName.Length > 0 ? prefix + secondaryName : string.Empty ),
                            category = category,
                            difficulty = difficulty,
                            originalDifficulty = difficulty,
                            hash = hash,
                            canBeRolled = canBeRolled,
                        };
                        _trickData[category][difficulty].Add( newEntry );

                        //Debug.Log( string.Format( "Trick data calculated hash {0} from ({1}, {2}, {3})", hash, category, prefix, name ) );

                        if( trickDataHashMap.ContainsKey( hash ) )
                        {
                            Debug.LogError( string.Format( "Trick data hash collision {0} from ({1}, {2}, {3})", hash, category, prefix, name ) );
                            continue;
                        }

                        trickDataHashMap.Add( hash, newEntry );
                    }
                }
            }
        }

        Debug.Log( "DataHandler::Initialise - Load ShortTrickNames" );


        // Load short trick name replacements
        using( var dbcmd = dbcon.CreateCommand() )
        {
            dbcmd.CommandText = "SELECT * from ShortTrickNames";
            using var reader = dbcmd.ExecuteReader();

            while( reader.Read() )
                _shortTrickNameReplacements.Add( new Pair<string, string>( reader.GetString( 0 ), reader.GetString( 1 ) ) );
        }

        Debug.Log( "DataHandler::Initialise - Load Challenges" );

        // Load challenges
        using( var dbcmd = dbcon.CreateCommand() )
        {
            dbcmd.CommandText = "SELECT * from Challenges";
            using var reader = dbcmd.ExecuteReader();

            while( reader.Read() )
            {
                var name = reader.GetString( 0 );
                var tricks = reader.GetString( 2 ).Split( new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries ).ToList();
                var trickList = new List<TrickEntry>();

                var challengeHash = xxHashSharp.xxHash.CalculateHash( Encoding.ASCII.GetBytes( name ) );
                var challengeEntry = _challengesData.GetOrAdd( challengeHash );
                var challengeData = new ChallengeData()
                {
                    name = name,
                    difficulty = reader.GetInt32( 1 ),
                    person = reader.GetString( 3 ),
                    category = reader.GetString( 4 ),
                    index = challengeEntry.Count,
                    hash = challengeHash,
                };

                foreach( var trick in tricks )
                {
                    var trickHash = xxHashSharp.xxHash.CalculateHash( Encoding.ASCII.GetBytes( challengeData.category + trick ) );

                    if( !trickDataHashMap.ContainsKey( trickHash ) )
                    {
                        Debug.LogError( string.Format( "Failed to find trick entry from hash for challenge {0} - {3} from ({1}, {2})", name, challengeData.category, trick, challengeData.person ) );
                        continue;
                    }

                    trickList.Add( trickDataHashMap[trickHash] );
                }

                //var total = 0.0f;
                //foreach( var x in trickList )
                //    total += 1.0f + Mathf.Pow( ( x.difficulty - 1.0f ) / 10.0f, 3.0f ) * 9.0f;
                //Debug.Log( string.Format( "Challenge: {0}-{1}, average diff: {2}, num tricks: {3}", name, challengeData.person, total * 3.0f / trickList.Count, trickList.Count ) );

                challengeData.landedData = new BitArray( 64 );

                if( trickList.Count > 64 )
                {
                    Debug.LogError( string.Format( "Failed to add challenge data as there were more tricks than we can stoere (64): {0} - {1} from ({2})", name, challengeData.person, challengeData.category ) );
                    continue;
                }

                challengeData.landedData.Length = trickList.Count;
                challengeData.tricks = trickList.AsReadOnly();
                challengeEntry.Add( challengeData );
            }
        }

        SaveGameSystem.LoadGame( saveDataName );
        EventSystem.Instance.TriggerEvent( new DataLoadedEvent() );
        Debug.Log( "DataHandler::DataLoaded" );
    }

    private void InitSqliteFile( string dbName )
    {
        databasePath = Path.Combine( Application.persistentDataPath, dbName );
        string sourcePath = Path.Combine( Application.streamingAssetsPath, dbName );
        Debug.Log( string.Format( "DataHandler::InitSqliteFile - databasePath: {0}", databasePath ) );

        //if DB does not exist in persistent data folder (folder "Documents" on iOS) or source DB is newer then copy it
       // if( !System.IO.File.Exists( databasePath ) || ( System.IO.File.GetLastWriteTimeUtc( sourcePath ) > System.IO.File.GetLastWriteTimeUtc( databasePath ) ) )
        {
            if( sourcePath.Contains( "://" ) )
            {
                Debug.Log( string.Format( "DataHandler::InitSqliteFile - sourcePath: {0} (Android)", sourcePath ) );

                // Android  
                var downloadHandler = new DownloadHandlerBuffer();
                UnityWebRequest request = new UnityWebRequest( sourcePath )
                {
                    downloadHandler = downloadHandler,
                    timeout = 5
                };
                request.SendWebRequest();

                // Wait for download to complete - not pretty at all but easy hack for now 
                // and it would not take long since the data is on the local device.
                while( !request.isDone ) {; }

                if( string.IsNullOrEmpty( request.error ) && request.result == UnityWebRequest.Result.Success )
                {
                    Debug.Log( "DataHandler::InitSqliteFile - Writing to databasepath from streaming folder (Android)" );
                    System.IO.File.WriteAllBytes( databasePath, request.downloadHandler.data );
                }
                else
                {
                    Debug.LogError( "ERROR: " + request.error );
                }
            }
            else
            {
                // Mac, Windows, Iphone
                Debug.Log( string.Format( "DataHandler::InitSqliteFile - sourcePath: {0} (Mac, Windows, Iphone)", sourcePath ) );

                //validate the existens of the DB in the original folder (folder "streamingAssets")
                if( System.IO.File.Exists( sourcePath ) )
                {
                    //copy file - all systems except Android
                    System.IO.File.Copy( sourcePath, databasePath, true );
                    Debug.Log( "DataHandler::InitSqliteFile - Writing to databasepath from streaming folder (Mac, Windows, Iphone)" );
                }
                else
                {
                    Debug.LogError( "ERROR: The file DB named " + dbName + " doesn't exist in the StreamingAssets Folder, please copy it there." );
                }

            }

        }
    }

    private Utility.FunctionTimer saveTimer;

    public void Save( bool instantSave )
    {
        if( !instantSave )
        {
            saveTimer?.Stop();
            saveTimer = Utility.FunctionTimer.CreateTimer( 3.0f, () => Save( true ) );
        }
        else
        {
            SaveGameSystem.SaveGame( saveDataName );
        }
    }

    public void ClearSavedData()
    {
        foreach( var trick in TrickData )
        {
            trick.status = TrickEntry.Status.Default;
            trick.lands = 0;
        }

        foreach( var( key, data ) in _challengesData )
            foreach( var challenge in data )
                challenge.landedData.SetAll( false );

        EventSystem.Instance.TriggerEvent( new ResetSaveDataEvent() );
        Save( true );
    }

    public void ModifyTrickDifficulty( TrickEntry trick, bool increase )
    {
        var previousDifficulty = trick.difficulty;
        _trickData[trick.category][previousDifficulty].Remove( trick );
        trick.difficulty = Mathf.Clamp( trick.difficulty + ( increase ? 1 : -1 ), 1, 10 );
        _trickData[trick.category][trick.difficulty].Insert( 0, trick );

        EventSystem.Instance.TriggerEvent( new TrickDifficultyChangedEvent() { trick = trick, previousDifficulty = previousDifficulty } );

        Save( false );
    }

    void ISavableComponent.Serialise( BinaryWriter writer )
    {
        Int32 trickCount = 0;

        foreach( var trick in TrickData )
            if( trick.status != TrickEntry.Status.Default || trick.lands > 0 || trick.difficulty != trick.originalDifficulty )
                trickCount++;

        writer.Write( trickCount );

        foreach( var trick in TrickData )
        {
            if( trick.status != TrickEntry.Status.Default || trick.lands > 0 || trick.difficulty != trick.originalDifficulty )
            {
                writer.Write( trick.hash );
                writer.Write( trick.status == TrickEntry.Status.Banned );
                writer.Write( trick.status == TrickEntry.Status.Landed );
                writer.Write( ( short )trick.lands );
                writer.Write( ( char )trick.difficulty );
            }
        }

        Int32 challengeCount = 0;

        foreach( var( hash, challenges ) in ChallengesData )
            foreach( var challenge in challenges )
                if( challenge.landedData.Any() )
                    challengeCount++;

        writer.Write( challengeCount );

        foreach( var (hash, challenges) in ChallengesData )
        {
            foreach( var challenge in challenges )
            {
                if( challenge.landedData.Any() )
                {
                    writer.Write( hash );
                    writer.Write( ( char )challenge.index );
                    writer.Write( challenge.landedData.ToNumeral() );
                }
            }
        }
    }

    void ISavableComponent.Deserialise( BinaryReader reader )
    {
        var trickCount = reader.ReadInt32();

        for( int i = 0; i < trickCount; ++i )
        {
            var hash = reader.ReadUInt32();
            var banned = reader.ReadBoolean();
            var landed = reader.ReadBoolean();
            var lands = reader.ReadInt16();
            var modifiedDifficulty = reader.ReadChar();

            if( !trickDataHashMap.ContainsKey( hash ) )
            {
                Debug.LogError( "Failed to find saved trick entry with hash: " + hash.ToString() );
            }
            else
            {
                trickDataHashMap[hash].status = banned ? TrickEntry.Status.Banned : landed ? TrickEntry.Status.Landed : TrickEntry.Status.Default;
                trickDataHashMap[hash].lands = lands;
                trickDataHashMap[hash].difficulty = modifiedDifficulty;
            }
        }

        var challengeCount = reader.ReadInt32();

        for( int i = 0; i < challengeCount; ++i )
        {
            var hash = reader.ReadUInt32();
            var index = reader.ReadChar();
            var landedData = reader.ReadInt64();
            var entry = _challengesData[hash][index];
            entry.landedData = landedData.ToBitArray();
            entry.landedData.Length = entry.tricks.Count;
        }
    }
}
                                                                                                                                                                                     