using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public int id;
    public string username;
    public CharacterController controller;
    public Transform shootOrigin;
    public float gravity = -9.81f;
    public float moveSpeed = 5f;
    public float jumpSpeed = 5f;
    public float throwForce = 600f;
    public float health;
    public float maxHealth = 100f;
    public int itemAmount = 0;
    public int maxItemAmount = 3;
    public int killCount = 0;
    [HideInInspector] public int spawnCount = 0;
    [HideInInspector] public PlayerColors player_color;
    [HideInInspector] public bool isWalking = false;
    [HideInInspector] public bool isJumping = false;
    [HideInInspector] public bool isGround = false;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRad = 0.4f;
    [SerializeField] private LayerMask groundLayer;

    [HideInInspector] public bool willPlayAgain = false;

    private bool[] inputs;
    private float yVelocity = 0;

    private void Start()
    {
        gravity *= Time.fixedDeltaTime * Time.fixedDeltaTime;
        moveSpeed *= Time.fixedDeltaTime;
        jumpSpeed *= Time.fixedDeltaTime;
    }

    public void Initialize(int _id, string _username)
    {
        id = _id;
        username = _username;
        health = maxHealth;
        // assigns this 'Player' reference to the list
        NetworkManager.instance.playerList.Add(this);
        // initialize movements
        inputs = new bool[5];
        // randomnly select color
        player_color = NetworkManager.instance.PlayerSelectColor();
    }

    /// <summary>Processes player input and moves the player.</summary>
    public void FixedUpdate()
    {
        
        //Checks if the player is on the ground; checks both feet(left and right)
        isGround = 
            Physics.CheckSphere(groundCheck.position, groundCheckRad, groundLayer) 
                ? true : false;

        if (health <= 0f)
        {
            return;
        }

        Vector2 _inputDirection = Vector2.zero;
        if (inputs[0])
        {
            _inputDirection.y += 1;
        }
        if (inputs[1])
        {
            _inputDirection.y -= 1;
        }
        if (inputs[2])
        {
            _inputDirection.x -= 1;
        }
        if (inputs[3])
        {
            _inputDirection.x += 1;
        }

        isWalking = (inputs[0] || inputs[1] || inputs[2] || inputs[3]) ? true : false;
        isJumping = (inputs[4]) ? true : false;

        Move(_inputDirection);
    }

    /// <summary>Calculates the player's desired movement direction and moves him.</summary>
    /// <param name="_inputDirection"></param>
    private void Move(Vector2 _inputDirection)
    {
        Vector3 _moveDirection = transform.right * _inputDirection.x + transform.forward * _inputDirection.y;
        _moveDirection *= moveSpeed;
        // if the player is on the ground
        if (controller.isGrounded)
        {
            yVelocity = 0f;
            // if jump key is pressed
            if (inputs[4])
            {
                yVelocity = jumpSpeed;
            }
        }
        // constantly add gravity to our player
        yVelocity += gravity;
        // update our y position
        _moveDirection.y = yVelocity;
        // updates our new transform to the CharacterController
        controller.Move(_moveDirection);

        ServerSend.PlayerPosition(this);
        ServerSend.PlayerRotation(this);
    }

    /// <summary>Updates the player input with newly received input.</summary>
    /// <param name="_inputs">The new key inputs.</param>
    /// <param name="_rotation">The new rotation.</param>
    public void SetInput(bool[] _inputs, Quaternion _rotation)
    {
        inputs = _inputs;
        transform.rotation = _rotation;
    }
    public void Shoot(Vector3 _viewDirection)
    {
        if (health <= 0f)
        {
            return;
        }
        // cast a ray and see it hits a player; distance of the bullet 25ft
        if (Physics.Raycast(shootOrigin.position, _viewDirection, out RaycastHit _hit, 25f))
        {
            if (_hit.collider.CompareTag("Player"))
            {
                _hit.collider.GetComponent<Player>().TakeDamage(50f, this);
            }
        }
    }
    
    public void ThrowItem(Vector3 _viewDirection)
    {
        if (health <= 0f)
        {
            return;
        }

        if (itemAmount > 0)
        {
            itemAmount--;
            var projectile = NetworkManager.instance.InstantiateProjectile(shootOrigin);
            projectile.Initialize(_viewDirection, throwForce, id);
            projectile.shooter = this;
        }
    }

    public void TakeDamage(float _damage, Player _playerShooter)
    {
        // if player taking damage has no more health
        if (health <= 0f)
        {
            return;
        }
        // if there's a friendly and self-fire
        if (id == _playerShooter.id)
        {
            return;
        }
        health -= _damage;
        if (health <= 0f)
        {
            health = 0f;
            controller.enabled = false;
            // respawn back the player randomnly
            int rand = Random.Range(0, GameObjectHandler.instance.playerSpawns.Count);
            transform.position = GameObjectHandler.instance.playerSpawns[rand].transform.position;
            ServerSend.PlayerPosition(this);
            // increment kill count to the killer
            _playerShooter.killCount++;
            ServerSend.PlayerKillPoint(_playerShooter);

            StartCoroutine(Respawn());
        }

        ServerSend.PlayerHealth(this);
    }

    private IEnumerator Respawn()
    {
        yield return new WaitForSeconds(5f);

        health = maxHealth;
        controller.enabled = true;
        ServerSend.PlayerRespawned(this);
    }

    public bool AttemptPickupItem()
    {
        if (itemAmount >= maxItemAmount)
        {
            return false;
        }

        itemAmount++;
        return true;
    }
}