using UnityEngine;
using System.Data;
using Mono.Data.Sqlite;
using System.IO;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;

public class DataHandler : MonoBehaviour
{
    public enum Flags
    {
        Default,
        Banned,
        Landed,
    }

    public struct TrickEntry
    {
        public int index;
        public string displayName;
        public bool landed;
        public bool banned;
        public string category;
        public int difficulty;
    }

    [HideInInspector] public List<TrickEntry> trickData = new List<TrickEntry>();
    [HideInInspector] public List<string> categories = new List<string>();
    [HideInInspector] public event Action onDataLoaded;
    [HideInInspector] public bool IsDataLoaded { get; private set; }

    private string databasePath;

    public void UpdateDatabaseTrick( TrickEntry entry )
    { 
    }

    private void Start()
    {
        var databaseName = "Database.db";

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
                categories.Add( reader.GetString( 0 ) );
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
                var landed = reader.GetInt32Safe( 4 ) > 0;
                var banned = reader.GetInt32Safe( 5 ) > 0;

                if( !categories.Contains( category ) )
                    Debug.LogError( name + " row from SQL database contains an invalid category: " + category );

                var displayName = name +
                    ( secondaryName.Length > 0 ? "\n(" + secondaryName + ")" : string.Empty ) +
                    ( "\nDifficulty: " + difficulty.ToString() );

                trickData.Add( new TrickEntry()
                {
                    displayName = displayName,
                    category = category,
                    difficulty = difficulty,
                    landed = landed,
                    banned = banned,
                } );
            }
        }

        IsDataLoaded = true;
        onDataLoaded?.Invoke();
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
}
                                                                                                                                                                                     