using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApp1
{
    public class Action
    {
        public string ActionName { get; set; }
        public string OrderType { get; set; }

        public Order ActionOrder { get; set; }
    }

    public class PropChangedComparer : IComparer<Order>
    {
        public int Compare(Order x, Order y)
        {
            if (y.SetAsLast) return -1;
            else return 0;
        }
    }

    public class Order : IComparable<Order>
    {
        public string OrderId { get; set; }
        public char Side { get; set; }
        public int Quantity { get; set; }
        public int Price { get; set; }
        public int NewQuantity { get; set; }
        public int NewPrice { get; set; }
        public bool SetAsLast { get; set; } = false;

        public int CompareTo(Order other)
        {
            if (this.Price > other.Price) return 1;
            if (this.Price < other.Price) return -1;
            else return 0;
        }
    }

    public static class MachingEngine
    {
        private static int tradedUnitsInAction = 0;

        private static List<Order> _buyOrders;
        private static List<Order> _sellOrders;

        static MachingEngine()
        {
            _buyOrders = new List<Order>();
            _sellOrders = new List<Order>();
        }

        private static void SortOrders()
        {
            _buyOrders.Sort(delegate (Order x, Order y)
            {
                return -x.Price.CompareTo(y.Price);
            });

            _sellOrders.Sort(delegate (Order x, Order y)
            {
                return x.Price.CompareTo(y.Price);
            });

        }

        private static void SortSellOrdersAfterModification()
        {
            _sellOrders.Sort(new PropChangedComparer());

            var order = _sellOrders.Where(o => o.SetAsLast = true).FirstOrDefault();

            if (order != null)
            {
                order.SetAsLast = false;
            }
        }

        private static void SortBuyOrdersAfterModification()
        {
            _buyOrders.Sort(new PropChangedComparer());

            var order = _buyOrders.Where(o => o.SetAsLast = true).FirstOrDefault();

            if(order != null)
            {
                order.SetAsLast = false;
            }
        }

        private static void SubtrackQuantity(Order subtractFrom, Order toSubtract)
        {
            subtractFrom.Quantity -= toSubtract.Quantity;

            if (toSubtract.Price == 0)
            {
                tradedUnitsInAction = tradedUnitsInAction + (toSubtract.Quantity * subtractFrom.Price);
            }
            else
            {
                tradedUnitsInAction = tradedUnitsInAction + (toSubtract.Quantity * toSubtract.Price);
            }
        }

        private static void SubtrackQuantityForIoC(Order subtractFrom, Order toSubtract)
        {
            var units = subtractFrom.Quantity - toSubtract.Quantity;

            if (units <= 0)
            {
                units = subtractFrom.Quantity;
                tradedUnitsInAction = tradedUnitsInAction + (units * subtractFrom.Price);

                subtractFrom.Quantity = 0;
            }
            else
            {
                subtractFrom.Quantity -= toSubtract.Quantity;
                tradedUnitsInAction = tradedUnitsInAction + (units * subtractFrom.Price);
            }
        }

        internal static void NewAction()
        {
            tradedUnitsInAction = 0;
        }

        internal static void SubmitLimitOrder(Order order)
        {
            SortOrders();

            switch (order.Side)
            {
                case 'B':
                    {
                        var sellOrder = _sellOrders.Where(o => order.Price >= o.Price).FirstOrDefault();

                        if (sellOrder == null)
                        {
                            _buyOrders.Add(order);
                            break;
                        }

                        if (order.Quantity >= sellOrder.Quantity)
                        {
                            SubtrackQuantity(order, sellOrder);

                            SubmitCancelOrder(sellOrder);

                            if (order.Quantity > 0) SubmitLimitOrder(order);
                        }
                        else
                        {
                            if (!_buyOrders.Exists(o => o.OrderId == order.OrderId)) _buyOrders.Add(order);
                        }

                        SortOrders();

                        break;
                    }
                case 'S':
                    {
                        var buyOrder = _buyOrders.Where(o => o.Price >= order.Price).FirstOrDefault();

                        if (buyOrder == null)
                        {
                            _sellOrders.Add(order);
                            break;
                        }

                        if (order.Quantity >= buyOrder.Quantity)
                        {
                            SubtrackQuantity(order, buyOrder);

                            SubmitCancelOrder(buyOrder);

                            if (order.Quantity > 0) SubmitLimitOrder(order);
                        }
                        else
                        {
                            if (!_sellOrders.Exists(o => o.OrderId == order.OrderId)) _sellOrders.Add(order);
                        }

                        SortOrders();

                        break;
                    }
                default:
                    {
                        Console.WriteLine("Incorrect order Side. Have to be Buy[B] or Sell[S]");
                        break;
                    }
            }
        }

        internal static void SubmitMarketOrder(Order order)
        {
            SortOrders();

            switch (order.Side)
            {
                case 'B':
                    {
                        var sellOrder = _sellOrders.FirstOrDefault();

                        if (sellOrder == null) break;

                        if (order.Quantity >= sellOrder.Quantity)
                        {
                            SubtrackQuantity(order, sellOrder);

                            SubmitCancelOrder(sellOrder);

                            if (order.Quantity > 0) SubmitMarketOrder(order);
                        }
                        else
                        {
                            SubtrackQuantity(sellOrder, order);
                        }

                        break;
                    }
                case 'S':
                    {
                        var buyOrder = _buyOrders.FirstOrDefault();

                        if (buyOrder == null) break;

                        if (order.Quantity >= buyOrder.Quantity)
                        {
                            SubtrackQuantity(order, buyOrder);

                            SubmitCancelOrder(buyOrder);

                            if (buyOrder.Quantity > 0) SubmitMarketOrder(order);

                            break;
                        }
                        else
                        {
                            SubtrackQuantity(buyOrder, order);
                        }

                        break;
                    }
                default:
                    {
                        Console.WriteLine("Incorrect order Side. Have to be Buy[B] or Sell[S]");
                        break;
                    }
            }
        }

        internal static void SubmitCancelOrder(Order order)
        {
            var toCancel = _buyOrders.Where(o => o.OrderId == order.OrderId).SingleOrDefault();

            if (toCancel != null)
            {
                _buyOrders.Remove(toCancel);
            }
            else
            {
                toCancel = _sellOrders.Where(o => o.OrderId == order.OrderId).SingleOrDefault();

                if (toCancel != null)
                {
                    _sellOrders.Remove(toCancel);
                }
            }
        }

        internal static void SubmitIocOrder(Order order)
        {
            SortOrders();

            switch (order.Side)
            {
                case 'B':
                    {
                        var sellOrder = _sellOrders.Where(o => order.Price >= o.Price).FirstOrDefault();

                        SubtrackQuantityForIoC(sellOrder, order);

                        if (sellOrder.Quantity <= 0) SubmitCancelOrder(sellOrder);

                        break;
                    }
                case 'S':
                    {
                        var buyOrder = _buyOrders.Where(o => o.Price >= order.Price).FirstOrDefault();

                        SubtrackQuantityForIoC(buyOrder, order);

                        if (buyOrder.Quantity <= 0) SubmitCancelOrder(buyOrder);

                        break;
                    }
                default:
                    {
                        Console.WriteLine("Incorrect order Side. Have to be Buy[B] or Sell[S]");
                        break;
                    }
            }
        }

        public static void SubmitFokOrder(Order order)
        {
            SortOrders();

            switch (order.Side)
            {
                case 'B':
                    {
                        var sellOrders = _sellOrders.Where(o => order.Price >= o.Price).ToList();

                        if (sellOrders.Count == 0) break;

                        int sumOfSellOrders = sellOrders.Sum(so => so.Quantity);

                        if (sumOfSellOrders >= order.Quantity)
                        {
                            foreach (var sellOrder in sellOrders)
                            {
                                order.Quantity -= sellOrder.Quantity;

                                if (order.Quantity >= 0)
                                {
                                    tradedUnitsInAction += sellOrder.Quantity * sellOrder.Price;
                                    sellOrder.Quantity = 0;
                                }
                                else
                                {
                                    tradedUnitsInAction += -order.Quantity * sellOrder.Price;
                                    sellOrder.Quantity = -order.Quantity;
                                    break;
                                }
                            }

                            foreach (var sellOrder in sellOrders)
                            {
                                if (sellOrder.Quantity == 0) SubmitCancelOrder(sellOrder);
                            }

                            break;
                        }
                        else
                        {
                            break;
                        }
                    }
                case 'S':
                    {
                        var buyOrders = _buyOrders.Where(o => o.Price >= order.Price).ToList();

                        if (buyOrders.Count == 0) break;

                        int sumOfSellOrders = buyOrders.Sum(so => so.Quantity);

                        if (sumOfSellOrders >= order.Quantity)
                        {
                            foreach (var sellOrder in buyOrders)
                            {
                                order.Quantity -= sellOrder.Quantity;

                                if (order.Quantity >= 0)
                                {
                                    tradedUnitsInAction += sellOrder.Quantity * sellOrder.Price;
                                    sellOrder.Quantity = 0;
                                }
                                else
                                {
                                    tradedUnitsInAction += (sellOrder.Quantity + order.Quantity) * sellOrder.Price;
                                    sellOrder.Quantity = -order.Quantity;
                                    break;
                                }
                            }

                            foreach (var sellOrder in buyOrders)
                            {
                                if (sellOrder.Quantity == 0) SubmitCancelOrder(sellOrder);
                            }

                            break;
                        }
                        else
                        {
                            break;
                        }
                    }
                default:
                    {
                        Console.WriteLine("Incorrect order Side. Have to be Buy[B] or Sell[S]");
                        break;
                    }
            }
        }

        private static Order ChangeOrder(Order oldOrder, Order newOrder)
        {
            if (oldOrder.Price == newOrder.NewPrice)
            {
                if (oldOrder.Quantity > newOrder.NewQuantity)
                {
                    oldOrder.Quantity = newOrder.NewQuantity;
                    
                    return null;
                }
            }
            else
            {
                newOrder.Price = newOrder.NewPrice;
                newOrder.Quantity = newOrder.NewQuantity;
                newOrder.SetAsLast = true;

                SubmitCancelOrder(oldOrder);
            }

            return newOrder;
        }

        public static void CancelReplaceLimitOrder(Order order)
        {
            bool isSellOrder = false;

            var oldOrder = _buyOrders.Where(o => o.OrderId == order.OrderId).FirstOrDefault();
            Order newOrder;

            if (oldOrder == null)
            {
                oldOrder = _sellOrders.Where(o => o.OrderId == order.OrderId).FirstOrDefault();

                if (oldOrder == null) return;

                isSellOrder = true;
                newOrder = ChangeOrder(oldOrder, order);
            }
            else
            {
                newOrder = ChangeOrder(oldOrder, order);
            }

            if(isSellOrder && newOrder != null)
            {
                _sellOrders.Add(newOrder);
                SortSellOrdersAfterModification();
            }
            else
            {
                _buyOrders.Add(newOrder);
                SortBuyOrdersAfterModification();
            }
        }

        internal static void ShowTrades()
        {
            Console.WriteLine(tradedUnitsInAction);
        }

        internal static void ShowSellOrders()
        {
            Console.Write("B: ");

            _buyOrders.RemoveAll(o => o == null);

            foreach (var b in _buyOrders)
            {
                Console.Write($"{b.Quantity}@{b.Price}#{b.OrderId}");
                Console.Write(" ");
            }
            Console.WriteLine();
        }

        internal static void ShowBuyOrders()
        {
            Console.Write("S: ");

            foreach (var s in _sellOrders)
            {
                Console.Write($"{s.Quantity}@{s.Price}#{s.OrderId}");
                Console.Write(" ");
            }
            Console.WriteLine();
        }

        internal static Action ReadAction(string actionParams)
        {
            var parameters = actionParams.Split(' ');

            switch (parameters.Length)
            {
                case 6:
                    {
                        return new Action
                        {
                            ActionName = parameters[0],
                            OrderType = parameters[1],
                            ActionOrder = new Order()
                            {
                                Side = char.Parse(parameters[2]),
                                OrderId = parameters[3],
                                Quantity = int.Parse(parameters[4]),
                                Price = int.Parse(parameters[5])
                            }
                        };
                    }
                case 5:
                    {
                        return new Action
                        {
                            ActionName = parameters[0],
                            OrderType = parameters[1],
                            ActionOrder = new Order()
                            {
                                Side = char.Parse(parameters[2]),
                                OrderId = parameters[3],
                                Quantity = int.Parse(parameters[4])
                            }
                        };
                    }
                case 4:
                    {
                        return new Action
                        {
                            ActionName = "CRP",
                            ActionOrder = new Order()
                            {
                                OrderId = parameters[1],
                                NewQuantity = int.Parse(parameters[2]),
                                NewPrice = int.Parse(parameters[3])
                            }
                        };
                    }
                case 2:
                    {
                        return new Action
                        {
                            ActionName = "CXL",
                            OrderType = "",
                            ActionOrder = new Order()
                            {
                                OrderId = parameters[1]
                            }
                        };
                    }
                case 1:
                    {
                        return new Action
                        {
                            ActionName = parameters[0],
                        };
                    }
                default:
                    {
                        Console.WriteLine("Wrong Action Params");
                        return null;
                    }
            }
        }
    }

    public class WindowManager
    {
        public static int Main(string[] args)
        {
            do
            {
                Action action = MachingEngine.ReadAction(Console.ReadLine());

                switch (action.ActionName)
                {
                    case "SUB":
                        {
                            MachingEngine.NewAction();

                            if (action.OrderType == "LO")
                            {
                                MachingEngine.SubmitLimitOrder(action.ActionOrder);
                            }

                            if (action.OrderType == "IOC")
                            {
                                MachingEngine.SubmitIocOrder(action.ActionOrder);
                            }

                            if (action.OrderType == "FOK")
                            {
                                MachingEngine.SubmitFokOrder(action.ActionOrder);
                            }

                            if (action.OrderType == "MO")
                            {
                                MachingEngine.SubmitMarketOrder(action.ActionOrder);
                            }

                            MachingEngine.ShowTrades();
                            MachingEngine.ShowSellOrders();
                            MachingEngine.ShowBuyOrders();

                            break;
                        }
                    case "CXL":
                        {
                            MachingEngine.NewAction();

                            MachingEngine.SubmitCancelOrder(action.ActionOrder);

                            MachingEngine.ShowTrades();
                            MachingEngine.ShowSellOrders();
                            MachingEngine.ShowBuyOrders();

                            break;
                        }
                    case "CRP":
                        {
                            MachingEngine.NewAction();

                            MachingEngine.CancelReplaceLimitOrder(action.ActionOrder);

                            MachingEngine.ShowTrades();
                            MachingEngine.ShowSellOrders();
                            MachingEngine.ShowBuyOrders();

                            break;
                        }
                    case "END":
                        {
                            MachingEngine.ShowSellOrders();
                            MachingEngine.ShowBuyOrders();
                            return 0;
                        }
                    default:
                        {
                            Console.WriteLine("Wrong Action Params");
                            break;
                        }
                }
            } while (true);

        }

    }
}
