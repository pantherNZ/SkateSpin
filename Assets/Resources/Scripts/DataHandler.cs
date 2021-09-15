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
        public int index;
        public string displayName;
        public bool landed;
        public bool banned;
        public string category;
        public int difficulty;
        public uint hash;
    }

    [HideInInspector] public List<TrickEntry> trickData = new List<TrickEntry>();
    [HideInInspector] Dictionary<uint, int> _trickDataHashMap = new Dictionary<uint, int>();
    public ReadOnlyDictionary<uint, int> trickDataHashMap
    {
        get { return new ReadOnlyDictionary<uint, int>( _trickDataHashMap ); }
    }

    [HideInInspector] List<string> _categories = new List<string>();
    public ReadOnlyCollection<string> categories
    {
        get { return _categories.AsReadOnly(); }
    }

    [HideInInspector] public event Action OnDataLoaded;
    [HideInInspector] public event Action OnResetSaveData;
    [HideInInspector] public bool IsDataLoaded { get; private set; }

    private string databasePath;

    static DataHandler _Instance;
    static public DataHandler Instance { get => _Instance; private set { } }

    private void Start()
    {
        _Instance = this;
        SaveGameSystem.AddSaveableComponent( this );
        Utility.FunctionTimer.CreateTimer( 0.01f, Initialise );
    }

    private const string saveDataName = "Data";
    private const string databaseName = "Database.db";

    private void Initialise()
    {
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

        // Load all tricks
        using( var dbcmd = dbcon.CreateCommand() )
        {
            dbcmd.CommandText = "SELECT * from Tricks";
            using var reader = dbcmd.ExecuteReader();

            while( reader.Read() )
            {
                var name = reader.GetStringSafe( 0 );
                var secondaryName = reader.GetStringSafe( 1 );
                var category = reader.GetStringSafe( 2 );
                var difficulty = reader.GetInt32Safe( 3 );

                if( !categories.Contains( category ) )
                    Debug.LogError( name + " row from SQL database contains an invalid category: " + category );

                var displayName = name +
                    ( secondaryName.Length > 0 ? "\n(" + secondaryName + ")" : string.Empty ) +
                    ( "\nDifficulty: " + difficulty.ToString() );

                var hash = xxHashSharp.xxHash.CalculateHash( Encoding.ASCII.GetBytes( name ) );
                var index = trickData.Count;

                trickData.Add( new TrickEntry()
                {
                    index = index,
                    displayName = displayName,
                    category = category,
                    difficulty = difficulty,
                    hash = hash,
                } );

                _trickDataHashMap.Add( hash, index );
            }
        }

        ClearSavedData();
        SaveGameSystem.LoadGame( saveDataName );
        IsDataLoaded = true;
        OnDataLoaded?.Invoke();
    }

    private void InitSqliteFile( string dbName )
    {
        databasePath = Path.Combine( Application.persistentDataPath, dbName );
        string sourcePath = Path.Combine( Application.streamingAssetsPath, dbName );

        //if DB does not exist in persistent data folder (folder "Documents" on iOS) or source DB is newer then copy it
        if( !System.IO.File.Exists( databasePath ) || ( System.IO.File.GetLastWriteTimeUtc( sourcePath ) > System.IO.File.GetLastWriteTimeUtc( databasePath ) ) )
        {

            if( sourcePath.Contains( "://" ) )
            {
                // Android  
                UnityWebRequest request = new UnityWebRequest( sourcePath );
                // Wait for download to complete - not pretty at all but easy hack for now 
                // and it would not take long since the data is on the local device.
                while( !request.isDone ) {; }

                if( string.IsNullOrEmpty( request.error ) )
                {
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

                //validate the existens of the DB in the original folder (folder "streamingAssets")
                if( System.IO.File.Exists( sourcePath ) )
                {

                    //copy file - alle systems except Android
                    System.IO.File.Copy( sourcePath, databasePath, true );

                }
                else
                {
                    Debug.LogError( "ERROR: The file DB named " + dbName + " doesn't exist in the StreamingAssets Folder, please copy it there." );
                }

            }

        }
    }

    public void Save()
    {
        SaveGameSystem.SaveGame( saveDataName );
    }

    public void ClearSavedData()
    {
        OnResetSaveData();
        foreach( var trick in trickData )
        {
            trick.banned = false;
            trick.landed = false;
        }

        Save();
    }

    void ISavableComponent.Serialise( BinaryWriter writer )
    {
        Int32 count = 0;

        foreach( var trick in trickData )
            if( trick.landed || trick.banned )
                count++;

        writer.Write( count );

        foreach( var trick in trickData )
        {
            if( trick.landed || trick.banned )
            {
                writer.Write( trick.hash );
                writer.Write( trick.landed );
                writer.Write( trick.banned );
            }
        }
    }

    void ISavableComponent.Deserialise( BinaryReader reader )
    {
        var count = reader.ReadInt32();

        for( int i = 0; i < count; ++i )
        {
            var hash = reader.ReadUInt32();
            var landed = reader.ReadBoolean();
            var banned = reader.ReadBoolean();

            if( !trickDataHashMap.ContainsKey( hash ) )
            {
                Debug.LogError( "Failed to find saved trick entry with hash: " + hash.ToString() );
            }
            else
            {
                trickData[trickDataHashMap[hash]].landed = landed;
                trickData[trickDataHashMap[hash]].banned = banned;
            }
        }
    }
}
                                                                                                                                                                                     