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
        public string category;
        public int difficulty;
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

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
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
        public int difficulty;
        public string category;
        public bool completed;
        public int index;
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

        // Load categories
        using( var dbcmd = dbcon.CreateCommand() )
        {
            dbcmd.CommandText = "SELECT * from Categories";
            using var reader = dbcmd.ExecuteReader();

            while( reader.Read() )
                _categories.Add( reader.GetString( 0 ) );
        }

        // Load difficulty names
        using( var dbcmd = dbcon.CreateCommand() )
        {
            dbcmd.CommandText = "SELECT * from DifficultyNames";
            using var reader = dbcmd.ExecuteReader();

            while( reader.Read() )
                _difficultyNames.Add( reader.GetInt32( 0 ), reader.GetString( 1 ) );
        }

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
                            name = prefix + name,
                            secondaryName = secondaryName.Length > 0 ? prefix + secondaryName : string.Empty,
                            category = category,
                            difficulty = difficulty,
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

        // Load short trick name replacements
        using( var dbcmd = dbcon.CreateCommand() )
        {
            dbcmd.CommandText = "SELECT * from ShortTrickNames";
            using var reader = dbcmd.ExecuteReader();

            while( reader.Read() )
                _shortTrickNameReplacements.Add( new Pair<string, string>( reader.GetString( 0 ), reader.GetString( 1 ) ) );
        }

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
        if( !System.IO.File.Exists( databasePath ) || ( System.IO.File.GetLastWriteTimeUtc( sourcePath ) > System.IO.File.GetLastWriteTimeUtc( databasePath ) ) )
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
        EventSystem.Instance.TriggerEvent( new ResetSaveDataEvent() );

        foreach( var trick in TrickData )
            trick.status = TrickEntry.Status.Default;

        Save( true );
    }

    void ISavableComponent.Serialise( BinaryWriter writer )
    {
        Int32 trickCount = 0;

        foreach( var trick in TrickData )
            if( trick.status != TrickEntry.Status.Default )
                trickCount++;

        writer.Write( trickCount );

        foreach( var trick in TrickData )
        {
            if( trick.status != TrickEntry.Status.Default )
            {
                writer.Write( trick.hash );
                writer.Write( ( char )trick.status );
            }
        }

        Int32 challengeCount = 0;

        foreach( var( hash, challenges ) in ChallengesData )
            foreach( var challenge in challenges )
                if( challenge.completed )
                    challengeCount++;

        writer.Write( challengeCount );

        foreach( var (hash, challenges) in ChallengesData )
        {
            foreach( var challenge in challenges )
            {
                if( challenge.completed )
                {
                    writer.Write( hash );
                    writer.Write( ( char )challenge.index );
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
            var status = reader.ReadChar();

            if( !trickDataHashMap.ContainsKey( hash ) )
            {
                Debug.LogError( "Failed to find saved trick entry with hash: " + hash.ToString() );
            }
            else
            {
                trickDataHashMap[hash].status = ( TrickEntry.Status )status;
            }
        }

        var challengeCount = reader.ReadInt32();

        for( int i = 0; i < challengeCount; ++i )
        {
            var hash = reader.ReadUInt32();
            var index = reader.ReadChar();
            ChallengesData[hash][index].completed = true;
        }
    }
}
                                                                                                                                                                                     