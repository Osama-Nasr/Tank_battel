using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Networking;
using Mirror;

public class TankMovement : NetworkBehaviour
{
    public Tilemap tilemap;
    public TileBase roadTile;
    public float speed = 5f;
    public float rotationSpeed = 5f;
    public Vector2 pivotOffset = new Vector2(0.5f, 0.5f);

    private Vector3Int currentCell;
    private Vector3Int nextCell;
    private Vector3Int testCell;
    private bool isMoving = false;
    [SyncVar]
    private bool isRotating = false;
    private float t = 0f;
    private Quaternion desiredRotation;
    [SyncVar]
    private int attempts = 0;
    private int maxAttempts = 10;
    [SyncVar]
    private int lastDirection = -1;
    [SyncVar]
    private Vector3Int vector3int_rec;
    [SyncVar]
    private bool tilebase_rec;
    [SyncVar]
    private Vector3 vector3_rec;
    //[SyncVar]
    //private bool tilebase_changed=false;
    [SyncVar]
    private bool isWaiting=false;

    //cmd for world to cell. There is a clientrpc needed as in gettile.
    [Command]
    void Cmdserver_WorldToCell(Vector3 position){
        Tilemap tilemap = GameObject.Find("Obstacles2").GetComponent<Tilemap>();
        //RpcReceive_worlToCell(tilemap.WorldToCell(position));
        vector3int_rec = tilemap.WorldToCell(position);
    }

    //This function is executed by the server to provide information about if the Cell sended is an obstacle or not. 
    [Command]
    void Cmdserver_GetTile(Vector3Int testCell, int direction){
        Tilemap tilemap = GameObject.Find("Obstacles2").GetComponent<Tilemap>(); //Take the tilemap
        TileBase tile = tilemap.GetTile(testCell); //Get the tile selected
        tilebase_rec = tile == roadTile; //Inow if the tile is an obstacle or not

        RpcChange_Rotating(tilebase_rec, direction); //Call the clientRpc function executed by the client to evaluate what the tank has to do
    }

    //This is the function executed by the client to know if he has to rotate or keep trying for different directions
    [ClientRpc]
    void RpcChange_Rotating(bool tilebase_rec, int direction){
        if (isOwned){ //hasAuthority
            if (tilebase_rec) //
            {
                isRotating = true;
                attempts = 0;
                lastDirection = direction;
            }
            else
            {
                attempts++;
            }
            isWaiting=false; //Client Update() function should stop waiting and start selecting a new direction or rotating
        }
        
    }

    //cmd for cell to world. There is a clientrpc needed as in gettile.
    [Command]
    void Cmdserver_CellToWorld(Vector3Int cell){
        Tilemap tilemap = GameObject.Find("Obstacles2").GetComponent<Tilemap>();
        vector3_rec = tilemap.CellToWorld(cell);
      
    }

    //This is really not executed by the server (It should be changed into a cmd executed by the server)
    Vector3 server_CellToWorld(Vector3Int cell){
        Tilemap tilemap = GameObject.Find("Obstacles2").GetComponent<Tilemap>();
        return tilemap.CellToWorld(cell);
    }


    private void Start()
    {
       if(this.isLocalPlayer){
            //currentCell = tilemap.WorldToCell(transform.position);
            Cmdserver_WorldToCell(transform.position);
            currentCell = vector3int_rec;
            Debug.Log(currentCell + " " + currentCell.ToString());
            nextCell = currentCell;
            testCell = currentCell;

            // Set initial position to the center of the tile
            Cmdserver_CellToWorld(currentCell);
            Vector3 cellCenter = vector3_rec + new Vector3(pivotOffset.x, pivotOffset.y, 0);
            //Vector3 cellCenter = server_CellToWorld(currentCell) + new Vector3(pivotOffset.x, pivotOffset.y, 0);
            //Vector3 cellCenter = tilemap.CellToWorld(currentCell) + new Vector3(pivotOffset.x, pivotOffset.y, 0);
            transform.position = cellCenter;
       }
    }

    private void Update()
    {
       if(isOwned){
            if (!isWaiting){  //If Waiting, the client will be waiting for the cmd and rpc of getTile to be executed.   
                if (!isMoving)
                {
                    if (!isRotating)
                    {
                        if (attempts < maxAttempts)
                        {
                            int direction = Random.Range(0, 4);

                            nextCell = currentCell;
                            testCell = currentCell;

                            if (lastDirection != -1 && Random.Range(0f, 1f) > 0.2f)
                            {
                                direction = lastDirection;
                            }

                            switch (direction)
                            {
                                case 0:
                                    desiredRotation = Quaternion.Euler(0f, 0f, 0f);
                                    nextCell.y += 1;
                                    break;
                                case 1:
                                    desiredRotation = Quaternion.Euler(0f, 0f, 180f);
                                    nextCell.y -= 1;
                                    break;
                                case 2:
                                    desiredRotation = Quaternion.Euler(0f, 0f, 90f);
                                    nextCell.x -= 1;
                                    break;
                                case 3:
                                    desiredRotation = Quaternion.Euler(0f, 0f, -90f);
                                    nextCell.x += 1;
                                    break;
                            }

                            testCell = nextCell;

                            //TileBase tile = tilemap.GetTile(testCell);
                            Cmdserver_GetTile(testCell, direction);
                            isWaiting=true; //Starts waiting for the cmd and rpc to end
                        }
                        else
                        {
                            attempts = 0;
                            return;
                        }
                        
                    }
                    else
                    {
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);

                        if (transform.rotation == desiredRotation)
                        {
                            // Check if there is another tank on the next cell
                            //Vector3 nextCellWorldPos = tilemap.CellToWorld(nextCell) + new Vector3(pivotOffset.x, pivotOffset.y, 0); //TRY TO CHANGE THIS INTO SERVER

                            //This does exactly the same as the previous, just on a separated function (still client)
                            Vector3 nextCellWorldPos = server_CellToWorld(nextCell)+ new Vector3(pivotOffset.x, pivotOffset.y, 0); //TRY TO CHANGE THIS INTO SERVER
                            
                            //Cmdserver_CellToWorld(nextCell);
                            //Vector3 nextCellWorldPos = vector3_rec + new Vector3(pivotOffset.x, pivotOffset.y, 0);
                            
                            if (!IsTankOnNextCell(nextCellWorldPos) && !IsTankRotatingTowardsCell(nextCellWorldPos))
                            {
                                isRotating = false;
                                isMoving = true;
                            }
                            else
                            {
                                isRotating = false;
                            }
                        }
                    }
                }

                if (isMoving)
                {
                    t += Time.deltaTime * speed;

                    // Calculate the world positions of the current and next cell centers
                    //Vector3 currentCellCenter = tilemap.CellToWorld(currentCell) + new Vector3(pivotOffset.x, pivotOffset.y, 0); //TRY TO CHANGE THIS INTO SERVER
                    //Vector3 nextCellCenter = tilemap.CellToWorld(nextCell) + new Vector3(pivotOffset.x, pivotOffset.y, 0); //TRY TO CHANGE THIS INTO SERVER

                    //This does exactly the same as the previous, just on a separated function (still client)
                    Vector3 currentCellCenter = server_CellToWorld(currentCell) + new Vector3(pivotOffset.x, pivotOffset.y, 0); //TRY TO CHANGE THIS INTO SERVER
                    Vector3 nextCellCenter = server_CellToWorld(nextCell)  + new Vector3(pivotOffset.x, pivotOffset.y, 0); //TRY TO CHANGE THIS INTO SERVER

                    /*Cmdserver_CellToWorld(currentCell);
                    Vector3 currentCellCenter = vector3_rec + new Vector3(pivotOffset.x, pivotOffset.y, 0);
                    Cmdserver_CellToWorld(nextCell);
                    Vector3 nextCellCenter = vector3_rec + new Vector3(pivotOffset.x, pivotOffset.y, 0);*/

                    // Lerp between the current and next cell centers
                    transform.position = Vector3.Lerp(currentCellCenter, nextCellCenter, t);

                    if (t >= 1f)
                    {
                        currentCell = nextCell;
                        isMoving = false;
                        t = 0f;
                    }
                }
            }
        }
    }

    private bool IsTankOnNextCell(Vector3 nextCellWorldPos)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(nextCellWorldPos, 0.1f);
        foreach (Collider2D collider in colliders)
        {
            if (collider.gameObject != this.gameObject && collider.GetComponent<TankMovement>() != null)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsTankRotatingTowardsCell(Vector3 nextCellWorldPos)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(nextCellWorldPos, 0.1f);
        foreach (Collider2D collider in colliders)
        {
            if (collider.gameObject != this.gameObject && collider.GetComponent<TankMovement>() != null && collider.GetComponent<TankMovement>().isRotating)
            {
                return true;
            }
        }
        return false;
    }
}