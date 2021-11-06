import React from 'react'
import ReactDOM from 'react-dom'
import App from './App'
import ServerDataContextProvider from './Context/ServerDataContextProvider'

ReactDOM.render(
  <ServerDataContextProvider>
    <App/>
  </ServerDataContextProvider>,
  document.getElementById('root')
)
