import {useContext, useEffect} from 'react'
import api from '../api'
import {ServerDataContext} from '../Context/ServerDataContextProvider'
import './style.css'

const Header = () => {
  const {staticServerInfo, setStaticServerInfo} = useContext(ServerDataContext)

  useEffect(async () => {
    const fetchedInfo = await api.getServerInfo()
    setStaticServerInfo(fetchedInfo)
  }, [])

  if (!staticServerInfo) return <></>

  const serverName = staticServerInfo?.name?.split('ℹ')

  return <div className='header'>
    <div className='header-content'>
      <div>{serverName}</div>
      <div>{staticServerInfo.ip}:{staticServerInfo.port}</div>
      <div>{staticServerInfo.clients} / {staticServerInfo.maxclients}</div>
      <button onClick={() => navigator.clipboard.writeText(`https://acstuff.ru/s/q:race/online/join?ip=${staticServerInfo.ip}&httpPort=${staticServerInfo.port}`)}>Copy Server Link</button>
    </div>
  </div>
}

export default Header