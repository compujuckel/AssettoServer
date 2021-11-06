import PlayerList from './PlayerList'
import './App.css'
import Header from './Header'

const App = () => {
  return <div className='app'>
    <Header/>
    <div>
      <PlayerList/>
    </div>
  </div>
}

export default App
