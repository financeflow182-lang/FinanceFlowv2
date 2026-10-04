# Frontend: o que mudar para suportar Receitas

O backend já tem receitas, e o cálculo do saldo agora usa **salário + receitas − gastos − investimentos**. O frontend só precisa consumir os campos novos e ganhar uma página de Receitas.

## 1. Tipos

```ts
// novo
type Income = {
  id: number;
  description: string;
  category: string;
  amount: number;
  date: string;      // "YYYY-MM-DD"
  createdAt: string;
};

// adicionar nos tipos existentes
type Budget = {
  // ...campos atuais
  otherIncome: number;  // soma das receitas lançadas no mês
  totalIncome: number;  // salary + otherIncome
};

type MonthlyTrend = {
  // ...campos atuais
  otherIncome: number;
};
```

`salary` continua sendo só o salário base, e `balance` já vem calculado pelo backend.

## 2. Endpoints novos

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/incomes?year=&month=&category=` | Lista (filtros opcionais) |
| GET | `/api/incomes/categories` | Lista de categorias (`string[]`) |
| GET | `/api/incomes/{id}` | Uma receita |
| POST | `/api/incomes` | Cria `{ description, category, amount, date }` |
| PUT | `/api/incomes/{id}` | Atualiza (mesmo corpo do POST) |
| DELETE | `/api/incomes/{id}` | Remove (204) |

Categorias válidas: Freela, Venda, 13º Salário, Férias, Reembolso, Rendimentos, Presente, Outros. Use o endpoint de categorias em vez de fixar a lista no front.

## 3. Página de Receitas (nova)

Copie a página de Gastos e simplifique:

- Remova o campo "recorrente".
- Troque o seletor de categoria (que vem de `/api/categories`) por um seletor com `/api/incomes/categories`.
- Mantenha filtro por mês, formulário, tabela e botões de editar e excluir.
- Adicione a rota e o item no menu lateral.

## 4. Dashboard

- No card do salário, mostrar a receita total (`totalIncome`). Opcional: detalhar "Salário R$ X + Outras receitas R$ Y".
- O card "Saldo livre" deve exibir `balance`, que já vem pronto. Não recalcule no front.
- No gráfico de tendência, `salary` é só o salário. Para exibir a receita do mês, use `salary + otherIncome`.
- `spendingPercent` agora é gastos ÷ receita total. Se o texto diz "do salário", troque por "da receita".

## 5. Cache e atualização

Se usar React Query, SWR ou similar: depois de criar, editar ou excluir uma receita, invalide as queries do orçamento (`/api/budgets/...`) e do dashboard (`/api/dashboard/...`). Sem isso o saldo só atualiza ao recarregar.

## 6. Formato dos erros

Erros de validação agora vêm como:

```json
{ "message": "A senha deve conter pelo menos um número.", "errors": ["..."] }
```

Exiba `message` (ou a lista `errors`). Se o front lê `title` ou o formato padrão do ASP.NET, ajuste, principalmente nas telas de login e cadastro.

## 7. Mensagens que podem aparecer

- Cadastro: "Este e-mail já está cadastrado."
- Login: "E-mail ou senha incorretos." e "Muitas tentativas de login. Tente novamente em 15 minutos." (HTTP 429)
- Refresh: "Sessão expirada. Faça login novamente." (HTTP 401). Ao receber isso, deslogue e redirecione para o login.
- Receitas: "Categoria de receita inválida. Use: ..." e "Informe um valor maior que zero."

## 8. Gastos fixos no dashboard

Esse ponto não exige mudança no front. Os gastos recorrentes agora entram nos totais do dashboard, do orçamento e dos alertas, na mesma regra da aba de Gastos. Se o dashboard continuar zerado depois do deploy, confirme que o backend com essa correção está no ar.

## Checklist

- [ ] Tipos atualizados (`Income`, `Budget`, `MonthlyTrend`)
- [ ] Funções de API para `/api/incomes`
- [ ] Página de Receitas e item no menu
- [ ] Dashboard usando `totalIncome` e `balance`
- [ ] Textos "salário" → "receita" onde couber
- [ ] Invalidação de cache após mudar receitas
- [ ] Tratamento de erro lendo `message`
