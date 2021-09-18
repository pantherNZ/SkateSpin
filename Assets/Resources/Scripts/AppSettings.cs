using System;
using System.IO;
using UnityEngine;

public class AppSettings : MonoBehaviour, ISavableComponent, IEventReceiver
{
    static AppSettings _Instance;
    static public AppSettings Instance
    {
        get => _Instance;
        private set { }
    }

    public bool useShortTrickNames;
    public bool alternateTrickNamesEnabled;

    private void Start()
    {
        _Instance = this;
        EventSystem.Instance.AddSubscriber( this );
        SaveGameSystem.AddSaveableComponent( this );
    }

    private void Awake()
    {
        EventSystem.Instance.TriggerEvent( new UseShortTrickNamesEvent() { value = useShortTrickNames } );
        EventSystem.Instance.TriggerEvent( new AlternateTrickNamesEvent() { value = alternateTrickNamesEnabled } );
    }

    void IEventReceiver.OnEventReceived( IBaseEvent e )
    {
        if( e.GetType() == typeof( UseShortTrickNamesEvent ) )
        {
            useShortTrickNames = ( ( UseShortTrickNamesEvent )e ).value;
            DataHandler.Instance.Save();
        }
        else if( e.GetType() == typeof( AlternateTrickNamesEvent ) )
        {
            alternateTrickNamesEnabled = ( ( AlternateTrickNamesEvent )e ).value;
            DataHandler.Instance.Save();
        }
    }

    void ISavableComponent.Serialise( BinaryWriter writer )
    {
        writer.Write( useShortTrickNames );
        writer.Write( alternateTrickNamesEnabled );
    }

    void ISavableComponent.Deserialise( BinaryReader reader )
    {
        useShortTrickNames = reader.ReadBoolean();
        alternateTrickNamesEnabled = reader.ReadBoolean();

        EventSystem.Instance.TriggerEvent( new UseShortTrickNamesEvent() { value = useShortTrickNames } );
        EventSystem.Instance.TriggerEvent( new AlternateTrickNamesEvent() { value = alternateTrickNamesEnabled } );
    }
}