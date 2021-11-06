import React, {useState} from 'react'

export const ServerDataContext = React.createContext({})

export default ({children}) => {
  const [staticServerInfo, setStaticServerInfo] = useState(undefined)
  const [players, setPlayers] = useState(undefined)

  return <ServerDataContext.Provider value={{
    staticServerInfo, setStaticServerInfo,
    players, setPlayers
  }}>
    {children}
  </ServerDataContext.Provider>
}
