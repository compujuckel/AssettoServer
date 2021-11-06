import {useContext, useState} from 'react'
import api from '../api'
import {useInterval} from '@restart/hooks'
import './style.css'
import {ServerDataContext} from '../Context/ServerDataContextProvider'

const fetchPlayers = async (setPlayers, setError) => {
  try {
    const fetchedPlayers = await api.getPlayers()
    setError(undefined)
    setPlayers(fetchedPlayers.players)
  } catch (apiError) {
    setError('An error occurred while trying to fetch the players list')
  }
}

const PlayerList = () => {
  const {players, setPlayers} = useContext(ServerDataContext)
  const [error, setError] = useState(undefined)

  useInterval(
    () => fetchPlayers(setPlayers, setError),
    10000,
    false,
    true
  )

  if (!!error) return <div className='player-list'>
    <div>{error}</div>
  </div>

  if (!players || players.length < 1) return <div className='player-list'>
    <div>There currently are no players on the server</div>
  </div>

  return <div className='player-list'>
    {players.map((player, index) => {
      return <div key={index} className='player-entry'>
        <div className='info'>
          <div>{player.sessionId} - {player.name}</div>
          <div>{player.car}</div>
          <div>{player.skin?.split('/')[0]}</div>
          <div>{player.country}, <a target='_blank' href={`https://steamcommunity.com/profiles/${player.guid}`}>Profile</a></div>
        </div>
        <div className='buttons'>
          <button>Kick</button>
          <button>Ban</button>
          <button>Copy ID</button>
        </div>
      </div>
    })}
  </div>
}

export default PlayerList
