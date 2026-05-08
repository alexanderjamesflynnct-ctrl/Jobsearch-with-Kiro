import SwaggerUI from 'swagger-ui-react'
import 'swagger-ui-react/swagger-ui.css'

export default function SwaggerDocs() {
  return (
    <div className="swagger-ui-wrapper">
      <SwaggerUI url="http://localhost:8000/swagger.json" />
    </div>
  )
}
