using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class Controller : MonoBehaviour
{
    //GameObjects
    public GameObject board;
    public GameObject[] cops = new GameObject[2];
    public GameObject robber;
    public Text rounds;
    public Text finalMessage;
    public Button playAgainButton;

    //Otras variables
    Tile[] tiles = new Tile[Constants.NumTiles];
    private int roundCount = 0;
    private int state;
    private int clickedTile = -1;
    private int clickedCop = 0;
                    
    void Start()
    {        
        InitTiles();
        InitAdjacencyLists();
        state = Constants.Init;
    }
        
    //Rellenamos el array de casillas y posicionamos las fichas
    void InitTiles()
    {
        for (int fil = 0; fil < Constants.TilesPerRow; fil++)
        {
            GameObject rowchild = board.transform.GetChild(fil).gameObject;            

            for (int col = 0; col < Constants.TilesPerRow; col++)
            {
                GameObject tilechild = rowchild.transform.GetChild(col).gameObject;                
                tiles[fil * Constants.TilesPerRow + col] = tilechild.GetComponent<Tile>();                         
            }
        }
                
        cops[0].GetComponent<CopMove>().currentTile=Constants.InitialCop0;
        cops[1].GetComponent<CopMove>().currentTile=Constants.InitialCop1;
        robber.GetComponent<RobberMove>().currentTile=Constants.InitialRobber;           
    }

    private void InitAdjacencyLists()
    {   
        // Matriz de adyacencia
        int[,] matriu = new int[Constants.NumTiles, Constants.NumTiles];

        int boardSize = 8;

        // Rellenar la matriz de adyacencia
        for (int i = 0; i < Constants.NumTiles; i++)
        {
            int row = i / boardSize;
            int col = i % boardSize;

            // Arriba
            if (row > 0)
            {
                matriu[i, i - boardSize] = 1;
            }

            // Abajo
            if (row < boardSize - 1)
            {
                matriu[i, i + boardSize] = 1;
            }

            // Izquierda
            if (col > 0)
            {
                matriu[i, i - 1] = 1;
            }

            // Derecha
            if (col < boardSize - 1)
            {
                matriu[i, i + 1] = 1;
            }
        }

        // Pasar la matriz a la lista adjacency de cada Tile
        for (int i = 0; i < Constants.NumTiles; i++)
        {
            tiles[i].adjacency.Clear();

            for (int j = 0; j < Constants.NumTiles; j++)
            {
                if (matriu[i, j] == 1)
                {
                    tiles[i].adjacency.Add(j);
                }
            }
        }
    }

    //Reseteamos cada casilla: color, padre, distancia y visitada
    public void ResetTiles()
    {        
        foreach (Tile tile in tiles)
        {
            tile.Reset();
        }
    }

    public void ClickOnCop(int cop_id)
    {
        switch (state)
        {
            case Constants.Init:
            case Constants.CopSelected:                
                clickedCop = cop_id;
                clickedTile = cops[cop_id].GetComponent<CopMove>().currentTile;
                tiles[clickedTile].current = true;

                ResetTiles();
                FindSelectableTiles(true);

                state = Constants.CopSelected;                
                break;            
        }
    }

    public void ClickOnTile(int t)
    {                     
        clickedTile = t;

        switch (state)
        {            
            case Constants.CopSelected:
                //Si es una casilla roja, nos movemos
                if (tiles[clickedTile].selectable)
                {                  
                    cops[clickedCop].GetComponent<CopMove>().MoveToTile(tiles[clickedTile]);
                    cops[clickedCop].GetComponent<CopMove>().currentTile=tiles[clickedTile].numTile;
                    tiles[clickedTile].current = true;   
                    
                    state = Constants.TileSelected;
                }                
                break;
            case Constants.TileSelected:
                state = Constants.Init;
                break;
            case Constants.RobberTurn:
                state = Constants.Init;
                break;
        }
    }

    public void FinishTurn()
    {
        switch (state)
        {            
            case Constants.TileSelected:
                ResetTiles();

                state = Constants.RobberTurn;
                RobberTurn();
                break;
            case Constants.RobberTurn:                
                ResetTiles();
                IncreaseRoundCount();
                if (roundCount <= Constants.MaxRounds)
                    state = Constants.Init;
                else
                    EndGame(false);
                break;
        }

    }

    public void RobberTurn()
    {
        RobberMove robberMove = robber.GetComponent<RobberMove>();

        // Casilla actual del caco
        clickedTile = robberMove.currentTile;
        tiles[clickedTile].current = true;

        // Buscar casillas seleccionables para el caco
        FindSelectableTiles(false);

        // Guardar las casillas seleccionables en una lista
        List<int> selectableTiles = new List<int>();

        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i].selectable)
            {
                selectableTiles.Add(i);
            }
        }

        // Si no hay casillas seleccionables, el ladrón pierde el turno
        if (selectableTiles.Count == 0)
        {
            FinishTurn();
            return;
        }

        int cop0Tile = cops[0].GetComponent<CopMove>().currentTile;
        int cop1Tile = cops[1].GetComponent<CopMove>().currentTile;

        int bestTileIndex = selectableTiles[0];
        int bestMinimumDistance = -1;
        int bestTotalDistance = -1;

        for (int i = 0; i < selectableTiles.Count; i++)
        {
            int candidateTile = selectableTiles[i];

            // Evitamos que el ladrón elija voluntariamente una casilla ocupada por un policía
            if (candidateTile == cop0Tile || candidateTile == cop1Tile)
            {
                continue;
            }

            int distanceToCop0 = GetDistanceBetweenTiles(candidateTile, cop0Tile);
            int distanceToCop1 = GetDistanceBetweenTiles(candidateTile, cop1Tile);

            // Nos interesa maximizar la distancia al policía más cercano
            int minimumDistance = Mathf.Min(distanceToCop0, distanceToCop1);

            // Criterio secundario para desempatar:
            // si dos casillas tienen la misma distancia mínima,
            // elegimos la que tenga mayor distancia total a ambos policías.
            int totalDistance = distanceToCop0 + distanceToCop1;

            if (minimumDistance > bestMinimumDistance ||
                minimumDistance == bestMinimumDistance && totalDistance > bestTotalDistance)
            {
                bestMinimumDistance = minimumDistance;
                bestTotalDistance = totalDistance;
                bestTileIndex = candidateTile;
            }
        }

        // Mover al caco a la mejor casilla encontrada
        robberMove.MoveToTile(tiles[bestTileIndex]);

        // Actualizar currentTile del caco
        robberMove.currentTile = bestTileIndex;
    }

    public void EndGame(bool end)
    {
        if(end)
            finalMessage.text = "You Win!";
        else
            finalMessage.text = "You Lose!";
        playAgainButton.interactable = true;
        state = Constants.End;
    }

    public void PlayAgain()
    {
        cops[0].GetComponent<CopMove>().Restart(tiles[Constants.InitialCop0]);
        cops[1].GetComponent<CopMove>().Restart(tiles[Constants.InitialCop1]);
        robber.GetComponent<RobberMove>().Restart(tiles[Constants.InitialRobber]);
                
        ResetTiles();

        playAgainButton.interactable = false;
        finalMessage.text = "";
        roundCount = 0;
        rounds.text = "Rounds: ";

        state = Constants.Restarting;
    }

    public void InitGame()
    {
        state = Constants.Init;
         
    }

    public void IncreaseRoundCount()
    {
        roundCount++;
        rounds.text = "Rounds: " + roundCount;
    }

    private int GetDistanceBetweenTiles(int startIndex, int targetIndex)
    {
        bool[] visited = new bool[Constants.NumTiles];
        int[] distance = new int[Constants.NumTiles];

        Queue<int> nodes = new Queue<int>();

        visited[startIndex] = true;
        distance[startIndex] = 0;
        nodes.Enqueue(startIndex);

        while (nodes.Count > 0)
        {
            int currentIndex = nodes.Dequeue();

            if (currentIndex == targetIndex)
            {
                return distance[currentIndex];
            }

            foreach (int adjacentIndex in tiles[currentIndex].adjacency)
            {
                if (visited[adjacentIndex] == false)
                {
                    visited[adjacentIndex] = true;
                    distance[adjacentIndex] = distance[currentIndex] + 1;
                    nodes.Enqueue(adjacentIndex);
                }
            }
        }

        // En principio no debería ocurrir en un tablero conectado,
        // pero devolvemos un valor alto por seguridad.
        return int.MaxValue;
    }

    public void FindSelectableTiles(bool cop)
    {
        int indexcurrentTile;

        if (cop == true)
            indexcurrentTile = cops[clickedCop].GetComponent<CopMove>().currentTile;
        else
            indexcurrentTile = robber.GetComponent<RobberMove>().currentTile;

        // La ponemos rosa porque acabamos de hacer un reset
        tiles[indexcurrentTile].current = true;

        // Cola para el BFS
        Queue<Tile> nodes = new Queue<Tile>();

        Tile startTile = tiles[indexcurrentTile];

        startTile.visited = true;
        startTile.distance = 0;
        startTile.parent = null;

        nodes.Enqueue(startTile);

        // Si se está moviendo un policía, la casilla del otro policía queda bloqueada
        int blockedTile = -1;

        if (cop == true)
        {
            int otherCop;

            if (clickedCop == 0)
                otherCop = 1;
            else
                otherCop = 0;

            blockedTile = cops[otherCop].GetComponent<CopMove>().currentTile;
        }

        while (nodes.Count > 0)
        {
            Tile currentTile = nodes.Dequeue();

            // Si ya hemos llegado a la distancia máxima, no expandimos más
            if (currentTile.distance >= Constants.Distance)
                continue;

            foreach (int adjacentIndex in currentTile.adjacency)
            {
                // Un policía no puede pasar por la casilla ocupada por el otro policía
                if (cop == true && adjacentIndex == blockedTile)
                    continue;

                Tile adjacentTile = tiles[adjacentIndex];

                if (adjacentTile.visited == false)
                {
                    adjacentTile.visited = true;
                    adjacentTile.parent = currentTile;
                    adjacentTile.distance = currentTile.distance + 1;

                    // Evitamos que una ficha pueda moverse a su casilla actual
                    if (adjacentTile.numTile != indexcurrentTile)
                        adjacentTile.selectable = true;

                    nodes.Enqueue(adjacentTile);
                }
            }
        }
    }









}
