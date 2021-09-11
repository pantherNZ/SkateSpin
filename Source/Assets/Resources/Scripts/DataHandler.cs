using UnityEngine;
using System.Data;
using Mono.Data.Sqlite;
using System.IO;
using UnityEngine.Networking;

public class DataHandler : MonoBehaviour
{
    string databasePath;

    private void Start()
    {
        var databaseName = "Database.db";

        // Copy db file to local device persistent storage (if not already there)
        InitSqliteFile( databaseName );

        // Open database
        string connection = "URI=file:" + databasePath;
        using SqliteConnection dbcon = new SqliteConnection( connection );
        dbcon.Open();

        IDbCommand dbcmd;
        IDataReader reader;

        dbcmd = dbcon.CreateCommand();
        dbcmd.CommandText = "SELECT Name from Tricks";
        reader = dbcmd.ExecuteReader();

        while ( reader.Read() )
        {
            Debug.LogError( reader.GetString( 0 ) );
        }
    }

    private void InitSqliteFile( string dbName )
    {
        databasePath = System.IO.Path.Combine( Application.persistentDataPath, dbName );
        string sourcePath = System.IO.Path.Combine( Application.streamingAssetsPath, dbName );

        //if DB does not exist in persistent data folder (folder "Documents" on iOS) or source DB is newer then copy it
        if ( !System.IO.File.Exists( databasePath ) || ( System.IO.File.GetLastWriteTimeUtc( sourcePath ) > System.IO.File.GetLastWriteTimeUtc( databasePath ) ) )
        {

            if ( sourcePath.Contains( "://" ) )
            {
                // Android  
                UnityWebRequest request = new UnityWebRequest( sourcePath );
                // Wait for download to complete - not pretty at all but easy hack for now 
                // and it would not take long since the data is on the local device.
                while ( !request.isDone ) {; }

                if ( string.IsNullOrEmpty( request.error ) )
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
                if ( System.IO.File.Exists( sourcePath ) )
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
                                                                                                                                                                                     