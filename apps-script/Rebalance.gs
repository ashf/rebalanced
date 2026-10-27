/**
 * Tax-aware rebalancing for the "investment rebalance" Google Sheet.
 *
 * Adds a Rebalance menu. "Calculate new orders" reads the sheet's prices, current share counts, targets and the
 * INCLUDE CRYPTO / INCLUDE BANKS switches, solves the same linear program as the command-line tool
 * (src/ReBalanced.Application/Services/TaxAware), and writes each traded account's orders, in shares, into the
 * New Order column. Install: Extensions > Apps Script, paste this file, save, reload the sheet.
 */

const BAND = 0; // fraction of the portfolio a class may drift before taxable sells; 0 = exact targets

// tax-efficient homes (Bogleheads placement); the first home is preferred when there are two
const HOMES = {
  UsStocks: ['Taxable', 'Roth'], DevelopedStocks: ['Taxable'], EmergingStocks: ['Taxable'], Bonds: ['Roth'],
  Reit: ['Roth'], Bitcoin: ['Roth'], Ethereum: ['Roth'], Cash: ['Taxable', 'Roth'], RealEstate: [],
};
const CLASS_OF = {
  VTI: 'UsStocks', VEA: 'DevelopedStocks', VWO: 'EmergingStocks', BND: 'Bonds', TMBRX: 'Bonds', BITCOIN: 'Bitcoin',
  GBTC: 'Bitcoin', BTC: 'Bitcoin', ETHEREUM: 'Ethereum', ETHE: 'Ethereum', ETH: 'Ethereum', CASH: 'Cash',
  VNQ: 'Reit', VGSLX: 'Reit', PROPERTY: 'RealEstate',
};
const PREFERRED = {
  UsStocks: 'VTI', DevelopedStocks: 'VEA', EmergingStocks: 'VWO', Bonds: 'BND', Bitcoin: 'BTC', Ethereum: 'ETH',
  Cash: 'CASH', Reit: 'VNQ',
};
const DEFAULT_RATIOS = {usShare: 0.55, developedShare: 0.75, bondShare: 0.06, minimumRealEstate: 0.05};
const PENALTY = {outsideBand: 1000, insideBand: 1, taxableSell: 3, turnover: 0.01, location: 0.2, nonPreferred: 0.001};

function onOpen() {
  SpreadsheetApp.getUi().createMenu('Rebalance')
    .addItem('Calculate new orders', 'calculateNewOrders')
    .addItem('Clear new orders', 'clearNewOrders')
    .addToUi();
}

function calculateNewOrders() {
  const sheet = SpreadsheetApp.getActiveSheet();
  const range = sheet.getDataRange();
  const result = planFromGrid(range.getValues(), () => LinearOptimizationService.createEngine(), range.getFormulas());
  writeOrders(sheet, result.layout.newOrderColumn, result.clearRows, result.orders);
  SpreadsheetApp.getUi().alert('Rebalance', report(result), SpreadsheetApp.getUi().ButtonSet.OK);
}

function clearNewOrders() {
  const sheet = SpreadsheetApp.getActiveSheet();
  const grid = sheet.getDataRange().getValues();
  const layout = readLayout(grid);
  const start = (findCell(grid, 'Total Investment') || fail('Cell "Total Investment" not found')).row + 1;
  const rows = readSections(grid, start, layout).filter(s => s.tradable)
    .flatMap(s => s.positions.filter(p => p.cls !== 'Cash').map(p => p.row));
  writeOrders(sheet, layout.newOrderColumn, rows, []);
}

function writeOrders(sheet, column, clearRows, orders) {
  clearRows.forEach(row => sheet.getRange(row + 1, column + 1).clearContent());
  orders.forEach(order => sheet.getRange(order.row + 1, column + 1).setValue(order.shares));
}

// Everything below is plain JavaScript over the sheet's values, so it can be tested outside Google Sheets.

function planFromGrid(grid, createEngine, formulas) {
  const layout = readLayout(grid);
  const portfolio = readPortfolio(grid, layout, formulas || []);
  const accounts = portfolio.accounts.map(account => ({
    ...account,
    positions: account.positions.filter(p => portfolio.includeCrypto || !['Bitcoin', 'Ethereum'].includes(p.cls)),
  }));
  const clearRows = accounts.filter(a => a.tradable)
    .flatMap(a => a.positions.filter(p => p.cls !== 'Cash').map(p => p.row));

  const taxable = accounts.find(a => a.kind === 'Taxable');
  const bankTransfer = portfolio.includeBanks && taxable ? Math.max(0, portfolio.bankCash) : 0;
  if (bankTransfer > 0) taxable.positions.push(cashPosition(bankTransfer, -1));

  const before = valueByClass(accounts);
  const targets = allocationTargets(before, portfolio);
  const plan = rebalance(accounts, targets, BAND, createEngine);

  const orders = [];
  const unplaced = [];
  plan.trades.forEach(trade => {
    // selling a whole position uses the exact share count, so no fraction is left behind
    const shares = trade.sellAll ? -trade.qty : round(trade.amount / trade.price, 4);
    if (trade.row >= 0) orders.push({row: trade.row, shares});
    else unplaced.push(`${trade.account}: ${trade.amount > 0 ? 'buy' : 'sell'} ${money(Math.abs(trade.amount))} ` +
      `${trade.ticker} has no row in the sheet; add one and run again`);
  });

  return {
    layout, clearRows, orders, plan, accounts, portfolio, bankTransfer, before,
    warnings: portfolio.warnings.concat(plan.warnings, unplaced),
  };
}

function readLayout(grid) {
  const header = grid[0].map(text);
  const column = name => header.findIndex(h => h.toLowerCase() === name.toLowerCase());
  const layout = {
    newOrderColumn: column('New Order'), cashColumn: column('Difference'), targetPercentColumn: column('Target %'),
  };
  if (layout.newOrderColumn < 0) throw new Error('No "New Order" column in row 1');
  return layout;
}

function readPortfolio(grid, layout, formulas) {
  const find = label => findCell(grid, label) || fail(`Cell "${label}" not found`);
  const right = label => { const c = findCell(grid, label); return c ? number(grid[c.row][c.col + 1]) : null; };
  const marketCap = find('Market Cap');
  const sections = readSections(grid, find('Total Investment').row + 1, layout);
  const cashRow = findCell(grid, 'CASH', 0) || fail('No CASH row in the summary table');
  const warnings = [];

  return {
    accounts: sections.filter(s => s.kind !== 'Banks'),
    bankCash: sum(sections.filter(s => s.kind === 'Banks').flatMap(s => s.positions).map(p => p.qty * p.price)),
    cryptoAllocation: right('crypto allocation') || 0,
    bitcoinMarketCap: number(grid[marketCap.row + 1][marketCap.col + 1]) || 0,
    ethereumMarketCap: number(grid[marketCap.row + 2][marketCap.col + 1]) || 0,
    cashTarget: number(grid[cashRow.row][2]) || 0,
    includeCrypto: isOn(grid, 'INCLUDE CRYPTO'),
    includeBanks: isOn(grid, 'INCLUDE BANKS'),
    ratios: readRatios(grid, layout, formulas, warnings),
    warnings,
  };
}

// a number other than 0, or a ticked checkbox
function isOn(grid, label) {
  const c = findCell(grid, label);
  const value = c ? grid[c.row][c.col + 1] : null;
  return value === true || (number(value) || 0) !== 0;
}

// the ratios are constants inside the sheet's formulas, so they are worked back out from the calculated values
function readRatios(grid, layout, formulas, warnings) {
  const right = label => { const c = findCell(grid, label); return c ? number(grid[c.row][c.col + 1]) : null; };
  const target = ticker => {
    const c = findCell(grid, ticker, 0);
    return c && layout.targetPercentColumn >= 0 ? number(grid[c.row][layout.targetPercentColumn]) : null;
  };
  const ratio = (n, d) => n === null || d === null || !(d > 0) ? null : n / d;
  const pick = (name, value, fallback) => {
    if (value !== null && value >= 0 && value <= 1) return round(value, 6);
    warnings.push(`Could not read the ${name} from the sheet; using ${(fallback * 100).toFixed(2)}%`);
    return fallback;
  };
  const [us, dev, em] = ['VTI', 'VEA', 'VWO'].map(target);
  const crypto = findCell(grid, 'Actual Crypto Alloc');
  const cryptoInUse = crypto ? number(grid[crypto.row + 1][crypto.col]) : 0;
  const bonds = right('bond allocation');
  const cashAndProperty = right('cash + prop alloc');
  const reitCell = findCell(grid, 'reit allocation');
  const reit = right('reit allocation');
  const reitFormula = reitCell && formulas[reitCell.row] ? String(formulas[reitCell.row][reitCell.col + 1] || '') : '';
  const floorInFormula = (reitFormula.match(/>\s*([0-9]*\.?[0-9]+)/) || [])[1];
  const cash = right('cash allocation');
  const property = right('prop allocation');

  return {
    usShare: pick('US share of stocks', us === null || dev === null || em === null ? null : ratio(us, us + dev + em), DEFAULT_RATIOS.usShare),
    developedShare: pick('developed share of international', dev === null || em === null ? null : ratio(dev, dev + em), DEFAULT_RATIOS.developedShare),
    bondShare: pick('bond share', bonds === null || cashAndProperty === null || cryptoInUse === null
      ? null : ratio(bonds, 1 - cashAndProperty - cryptoInUse), DEFAULT_RATIOS.bondShare),
    // with real estate above the floor the reit cell is 0, so read the floor from its formula, e.g. IF(C14>0.05, ...)
    minimumRealEstate: pick('real estate floor', reit > 0
      ? (cash !== null && cash < 1 && property !== null ? reit / (1 - cash) + property : null)
      : (floorInFormula !== undefined ? Number(floorInFormula) : null), DEFAULT_RATIOS.minimumRealEstate),
  };
}

function readSections(grid, startRow, layout) {
  const sections = [];
  for (let row = startRow; row < grid.length; row++) {
    const title = text(grid[row][0]);
    // summary rows such as "Avg Buy Price" carry a number in column B; section headers do not
    if (!title || number(grid[row][1]) !== null) continue;

    const kind = kindOf(title, text(grid[row][1]));
    const tradable = kind === 'Taxable' || kind === 'RothIra';
    const positions = [];
    for (row++; row < grid.length && text(grid[row][0]).toLowerCase() !== 'total'; row++) {
      const position = readPosition(grid, row, kind, title, layout);
      // an empty row in a traded account stays, so orders can still be written into it
      if (position && (tradable || position.qty * position.price !== 0)) positions.push(position);
    }
    sections.push({
      name: kind === 'Taxable' ? 'Taxable' : title, kind,
      type: {Taxable: 'Taxable', Banks: 'Taxable', RothIra: 'Roth', Retirement401K: 'Roth'}[kind] || 'Other',
      tradable, positions,
    });
  }
  return sections;
}

function readPosition(grid, row, kind, section, layout) {
  const cells = grid[row];
  const label = text(cells[0]);
  if (!label) return null;
  const value = number(cells[5]) || 0;

  if (kind === 'Banks') return cashPosition(value, row);
  if (label.toUpperCase() === 'CASH') {
    // the account's cash before any new orders; Mkt Val already has the orders taken out
    const input = layout.cashColumn >= 0 ? number(cells[layout.cashColumn]) : null;
    if (input === null && kind !== 'Wallet')
      fail(`Row ${row + 1}: the CASH amount for ${section} must be in the Difference column`);
    return cashPosition(input !== null ? input : value, row);
  }
  if (kind === 'RealEstate') {
    if (!(number(cells[3]) > 0))
      fail(`Row ${row + 1}: "${section}" is not a known account section, and "${label}" has no quantity in column D`);
    return {ticker: label, cls: 'RealEstate', qty: 1, price: value, row};
  }

  const coin = /ethereum/i.test(section) ? 'ethereum' : 'bitcoin';
  const ticker = kind === 'Wallet' ? coin : label;
  const cls = CLASS_OF[ticker.toUpperCase()] || fail(`Unknown ticker ${label} in ${section}`);
  const qty = number(cells[3]) || 0;
  const price = number(cells[1]) || 0;
  if (qty !== 0 && !(price > 0)) fail(`Row ${row + 1}: ${label} in ${section} has no price`);
  return {ticker, cls, qty, price, row};
}

function kindOf(title, description) {
  const has = t => (title + ' ' + description).toLowerCase().includes(t.toLowerCase());
  if (has('Taxable')) return 'Taxable';
  if (has('401(k)')) return 'Retirement401K';
  if (has('Roth IRA')) return 'RothIra';
  if (has('Banks')) return 'Banks';
  if (has('Bitcoin') || has('Ethereum')) return 'Wallet';
  return 'RealEstate';
}

// mirrors the sheet's Target % formulas
function allocationTargets(valueByClass, portfolio) {
  const total = sum(Object.values(valueByClass));
  const caps = portfolio.bitcoinMarketCap + portfolio.ethereumMarketCap;
  if (!(total > 0)) fail('The portfolio total is not positive');
  if (portfolio.includeCrypto && !(caps > 0)) fail('Crypto is included but the market caps are missing');

  const r = portfolio.ratios;
  const realEstate = (valueByClass.RealEstate || 0) / total;
  const cash = portfolio.cashTarget / total;
  const reit = (1 - cash) * Math.max(0, r.minimumRealEstate - realEstate);
  const crypto = portfolio.includeCrypto ? portfolio.cryptoAllocation : 0;
  const remainder = 1 - cash - realEstate - reit - crypto;
  if (remainder < 0) fail('Cash, real estate and crypto add up to more than the whole portfolio');
  const bonds = remainder * r.bondShare;
  const stocks = remainder - bonds;

  const targets = {
    UsStocks: stocks * r.usShare, DevelopedStocks: stocks * (1 - r.usShare) * r.developedShare,
    EmergingStocks: stocks * (1 - r.usShare) * (1 - r.developedShare), Bonds: bonds, Cash: cash, Reit: reit,
    RealEstate: realEstate,
  };
  if (portfolio.includeCrypto) {
    targets.Bitcoin = crypto * portfolio.bitcoinMarketCap / caps;
    targets.Ethereum = crypto * portfolio.ethereumMarketCap / caps;
  }
  Object.keys(targets).forEach(k => targets[k] *= total);
  return targets;
}

// dollar-based linear program; see TaxAwareRebalancer.cs for the reasoning behind each term
function rebalance(accounts, targets, band, createEngine) {
  const engine = createEngine();
  engine.setMinimization();
  const total = sum(accounts.map(accountValue));
  const prices = {};
  accounts.forEach(a => a.positions.forEach(p => { if (p.price > 0 && !prices[p.ticker]) prices[p.ticker] = p.price; }));

  const tradable = accounts.filter(a => a.tradable);
  const targeted = Object.keys(targets).filter(c => c !== 'RealEstate' && targets[c] > 0);
  const homeless = targeted.filter(c => !tradable.some(a => HOMES[c].includes(a.type)));
  const warnings = homeless.map(c => `No traded account is a tax-efficient home for ${c}, so any account may hold it`);
  const allowed = (account, cls) => homeless.includes(cls) || HOMES[cls].includes(account.type);

  const fixed = {};
  const holdings = [];
  let n = 0;
  accounts.forEach(account => {
    if (!account.tradable) {
      account.positions.forEach(p => fixed[p.cls] = (fixed[p.cls] || 0) + p.qty * p.price);
      return;
    }
    const current = {};
    account.positions.forEach(p => {
      const h = current[p.ticker] || (current[p.ticker] = {ticker: p.ticker, cls: p.cls, value: 0, qty: 0, row: p.row});
      h.value += p.qty * p.price;
      h.qty += p.qty;
      if (h.row < 0) h.row = p.row;
    });
    targeted.concat(targeted.includes('Cash') ? [] : ['Cash']).filter(c => allowed(account, c)).forEach(cls => {
      const ticker = PREFERRED[cls];
      if (ticker && !current[ticker]) current[ticker] = {ticker, cls, value: 0, qty: 0, row: -1};
    });

    const accountTotal = engine.addConstraint(accountValue(account), accountValue(account));
    Object.values(current).forEach(h => {
      const id = n++;
      const finalName = `f${id}`, buyName = `b${id}`, sellName = `s${id}`;
      engine.addVariable(finalName, 0, allowed(account, h.cls) ? 1e15 : 0);
      engine.addVariable(buyName, 0, 1e15);
      engine.addVariable(sellName, 0, 1e15);
      const balance = engine.addConstraint(h.value, h.value);
      balance.setCoefficient(finalName, 1);
      balance.setCoefficient(buyName, -1);
      balance.setCoefficient(sellName, 1);
      accountTotal.setCoefficient(finalName, 1);

      // spending or receiving cash is the other side of a trade, not a trade
      if (h.cls !== 'Cash') {
        engine.setObjectiveCoefficient(buyName, PENALTY.turnover + (h.ticker === PREFERRED[h.cls] ? 0 : PENALTY.nonPreferred));
        engine.setObjectiveCoefficient(sellName, PENALTY.turnover + (account.type === 'Taxable' ? PENALTY.taxableSell : 0));
      }
      const homes = HOMES[h.cls];
      engine.setObjectiveCoefficient(finalName, homes.length > 1 && homes[0] !== account.type ? PENALTY.location : 0);
      holdings.push({account, ...h, finalName});
    });
  });

  const bandDollars = band * total;
  const classes = new Set(Object.keys(targets).concat(Object.keys(fixed), holdings.map(h => h.cls)));
  classes.forEach(cls => {
    const rhs = (targets[cls] || 0) - (fixed[cls] || 0);
    const deviation = engine.addConstraint(rhs, rhs);
    holdings.filter(h => h.cls === cls).forEach(h => deviation.setCoefficient(h.finalName, 1));
    [[1, bandDollars, PENALTY.insideBand], [-1, bandDollars, PENALTY.insideBand],
      [1, 1e15, PENALTY.outsideBand], [-1, 1e15, PENALTY.outsideBand]].forEach(([sign, upper, penalty]) => {
      const slack = `d${n++}`;
      engine.addVariable(slack, 0, upper);
      deviation.setCoefficient(slack, sign);
      engine.setObjectiveCoefficient(slack, penalty);
    });
  });

  const solution = engine.solve();
  // isValid() also accepts FEASIBLE, e.g. after the solver's time limit
  const optimal = typeof LinearOptimizationService === 'undefined' ? 'OPTIMAL' : LinearOptimizationService.Status.OPTIMAL;
  if (solution.getStatus() !== optimal) fail(`No optimal rebalance found (${solution.getStatus()})`);

  const after = {...fixed};
  const trades = [];
  holdings.forEach(h => {
    const final = solution.getVariableValue(h.finalName);
    after[h.cls] = (after[h.cls] || 0) + final;
    const amount = round(final - h.value, 2);
    if (h.cls !== 'Cash' && Math.abs(amount) >= 0.01)
      trades.push({
        account: h.account.name, ticker: h.ticker, amount, price: prices[h.ticker] || 0, row: h.row, qty: h.qty,
        sellAll: h.qty > 0 && final < 0.005,
      });
  });

  Object.keys(targets).forEach(cls => {
    const miss = (after[cls] || 0) - targets[cls];
    if (Math.abs(miss) > Math.max(band * total, 1))
      warnings.push(`${cls} ends ${money(Math.abs(miss))} ${miss > 0 ? 'over' : 'under'} its target: ` +
        'held accounts and placement rules leave no trade that closes the gap');
  });
  return {trades, after, targets, total, warnings};
}

function report(result) {
  const {plan, portfolio, bankTransfer, before} = result;
  const pct = (values, cls) => ((values[cls] || 0) / plan.total * 100).toFixed(2) + '%';
  const lines = [`Portfolio total ${money(plan.total)} (crypto ${portfolio.includeCrypto ? 'included' : 'held as is'}, ` +
    `bank cash ${portfolio.includeBanks ? 'included' : 'ignored'})`, ''];
  if (bankTransfer > 0) lines.push(`First transfer ${money(bankTransfer)} from your banks into Taxable, then add it to ` +
    'the Taxable CASH cell so the orders below are funded.', '');
  if (plan.trades.length === 0) lines.push('No trades needed.');
  plan.trades.slice().sort((a, b) => a.account.localeCompare(b.account) || a.amount - b.amount).forEach(t =>
    lines.push(`${t.account}: ${t.amount < 0 ? 'SELL' : 'BUY'} ${money(Math.abs(t.amount))} ${t.ticker}` +
      (t.price > 0 ? ` (~${Math.abs(t.amount / t.price).toFixed(3)} sh)` : '')));
  lines.push('', 'Class: target / before / after');
  Object.keys(plan.targets).forEach(c => lines.push(`${c}: ${pct(plan.targets, c)} / ${pct(before, c)} / ${pct(plan.after, c)}`));
  result.warnings.forEach(w => lines.push('', 'Warning: ' + w));
  if (plan.trades.some(t => t.account === 'Taxable' && t.amount < 0))
    lines.push('', 'Taxable sells realize capital gains or losses. Check each lot\'s cost basis before selling.');
  return lines.join('\n');
}

function findCell(grid, label, onlyColumn) {
  const wanted = label.toLowerCase();
  for (let row = 0; row < grid.length; row++)
    for (let col = 0; col < grid[row].length; col++)
      if ((onlyColumn === undefined || col === onlyColumn) && text(grid[row][col]).toLowerCase() === wanted)
        return {row, col};
  return null;
}

function cashPosition(value, row) { return {ticker: 'CASH', cls: 'Cash', qty: value, price: 1, row}; }
function valueByClass(accounts) {
  const values = {};
  accounts.forEach(a => a.positions.forEach(p => values[p.cls] = (values[p.cls] || 0) + p.qty * p.price));
  return values;
}
function accountValue(account) { return sum(account.positions.map(p => p.qty * p.price)); }
function text(value) { return typeof value === 'string' ? value.trim() : ''; }
function number(value) { return typeof value === 'number' && isFinite(value) ? value : null; }
function sum(values) { return values.reduce((a, b) => a + b, 0); }
function round(value, digits) { const f = Math.pow(10, digits); return Math.round(value * f) / f; }
function money(value) { return '$' + value.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ','); }
function fail(message) { throw new Error(message); }
