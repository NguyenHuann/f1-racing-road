using UnityEngine;

public class powerup : MonoBehaviour {

    public PowerupType powerupType = PowerupType.nitrus;

    void Start() {
        int RandIndex = Random.Range(0, 3);
        switch (RandIndex) {
            case 0: powerupType = PowerupType.nitrus; break;
            case 1: powerupType = PowerupType.rocket; break;
            case 2: powerupType = PowerupType.shield; break;

        }
    }

    void OnTriggerEnter(Collider other) {
        //print("collieded with this object !" + other.name);
        var _playerStateMachine = other.transform.parent.GetComponent<CarStateMachine>();
        if (_playerStateMachine) {
            Powerup _powerup = new() {
                type = powerupType
            };
            if (_playerStateMachine.AddPowerup(_powerup)) {
                Destroy(gameObject);
            }
            print("adding powerup to car !");
        } else {
            print("collider is not a player type !!!!");
        }
    }
}
