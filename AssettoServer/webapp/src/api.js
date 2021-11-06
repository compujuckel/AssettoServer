const SERVER_HOST = String(process.env.REACT_APP_SERVER_HOST)

const getPlayers = async (password) => {
  const payload = _createPayload({})
  const result = await fetch(`${SERVER_HOST}/api/players`, payload)
  return await result.json()
}

const getServerInfo = async () => {
  const payload = _createPayload({})
  const result = await fetch(`${SERVER_HOST}/INFO`, payload)
  return await result.json()
}

const _createPayload = ({body, method = 'GET', headers = {'Content-Type': 'application/json'}}) => {
  return {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined
  }
}

export default {
  getPlayers,
  getServerInfo
}