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
    public bool canPickLandedTricks;

    private void Awake()
    {
        _Instance = this;
        EventSystem.Instance.AddSubscriber( this );
        SaveGameSystem.AddSaveableComponent( this );
    }

    private void Start()
    {
        EventSystem.Instance.TriggerEvent( new UseShortTrickNamesEvent() { value = useShortTrickNames }, this );
        EventSystem.Instance.TriggerEvent( new CanPickLandedTricksEvent() { value = canPickLandedTricks }, this );
    }

    void IEventReceiver.OnEventReceived( IBaseEvent e )
    {
        if( e is UseShortTrickNamesEvent useShortTrickNamesEvent )
        {
            useShortTrickNames = useShortTrickNamesEvent.value;
            DataHandler.Instance.Save( false );
        }
        else if( e is CanPickLandedTricksEvent canPickLandedTricksEvent )
        {
            canPickLandedTricks = canPickLandedTricksEvent.value;
            DataHandler.Instance.Save( false );
        }
        else if( e is ResetSaveDataEvent )
        {
            useShortTrickNames = false;
            canPickLandedTricks = false;
        }
    }

    void ISavableComponent.Serialise( BinaryWriter writer )
    {
        writer.Write( useShortTrickNames );
        writer.Write( canPickLandedTricks );
    }

    void ISavableComponent.Deserialise( int saveVersion, BinaryReader reader )
    {
        useShortTrickNames = reader.ReadBoolean();
        canPickLandedTricks = reader.ReadBoolean();
        EventSystem.Instance.TriggerEvent( new UseShortTrickNamesEvent() { value = useShortTrickNames }, this );
        EventSystem.Instance.TriggerEvent( new CanPickLandedTricksEvent() { value = canPickLandedTricks }, this );
    }
}