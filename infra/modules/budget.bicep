// Alerta de presupuesto sobre el resource group — Fase 0 del despliegue a Azure
// Container Apps. Deliberadamente NO usa Microsoft.Consumption/budgets (el tipo de
// recurso "clásico"): a la fecha de este despliegue, Microsoft.CostManagement/budgets
// es el namespace más nuevo y activamente mantenido, sin que el otro esté deprecado —
// ver la nota de investigación en el plan de Fase 0. Si en el futuro
// Microsoft.Consumption/budgets deja de aceptar despliegues nuevos, este es el
// reemplazo directo (mismo shape de propiedades).
//
// Esto es SOLO una alerta (notificación por email a los umbrales); no corta ni
// deshabilita nada por sí sola. El corte real, si la suscripción es de un tipo con
// crédito (Free Trial / Azure for Students / crédito de Visual Studio), lo hace el
// "límite de gasto" de la suscripción — un toggle de cuenta que no se puede activar por
// Bicep ni por CLI, solo desde el portal. El script de preflight (scripts/
// preflight-budget-check.*) verifica que esté prendido y aborta el despliegue si no.

@description('Nombre del budget. Debe ser único dentro del resource group.')
param budgetName string

@description('Monto mensual del presupuesto, en USD. Tope duro pedido por el usuario: 1.')
param amount int = 1

@description('Email(s) que reciben la alerta en cada umbral.')
param contactEmails array

@description('Primer día del mes de inicio del presupuesto, formato YYYY-MM-DD. Default: primer día del mes actual (UTC).')
param startDate string = utcNow('yyyy-MM-01')

// Los presupuestos mensuales de Cost Management piden un endDate; usamos +10 años
// (el máximo permitido) para no tener que redesplegar esto cada ciertos meses.
@description('Último día de vigencia del presupuesto, formato YYYY-MM-DD.')
param endDate string = dateTimeAdd(startDate, 'P10Y', 'yyyy-MM-dd')

var thresholds = [50, 90, 100]

resource budget 'Microsoft.CostManagement/budgets@2025-03-01' = {
  name: budgetName
  properties: {
    category: 'Cost'
    amount: amount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: startDate
      endDate: endDate
    }
    notifications: {
      alerta50: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: thresholds[0]
        thresholdType: 'Actual'
        contactEmails: contactEmails
      }
      alerta90: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: thresholds[1]
        thresholdType: 'Actual'
        contactEmails: contactEmails
      }
      alerta100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: thresholds[2]
        thresholdType: 'Actual'
        contactEmails: contactEmails
      }
    }
  }
}

output budgetId string = budget.id
output budgetName string = budget.name
